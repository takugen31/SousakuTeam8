"""Check tracked CSV/Unity scenario agreement and current minigame contracts."""
import argparse, csv, json, re
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent

def norm(s): return re.sub('[^a-z0-9]', '', s.lower())
def scalar(s):
    s = s.strip()
    return json.loads(re.sub(r'\n[ \t]+', ' ', s)) if s.startswith('"') else s

def verify(mode):
    scenarios = {}; rows_by_id = {}; guid_paths = {}
    for p in (ROOT / 'Assets/Dialogue/Data').rglob('*.asset'):
        a = p.read_text(encoding='utf-8-sig')
        blocks = re.split(r'(?m)^  - lineId: ', a)[1:]
        if not blocks: continue
        meta = Path(str(p)+'.meta').read_text(encoding='utf-8-sig')
        guid_paths[re.search(r'^guid: (\w+)', meta, re.M)[1]] = p
        scenarios[norm(p.stem)] = (p, a, blocks)
    enabled = set(re.findall(r'enabled: 1\n    path: (.+)', (ROOT/'ProjectSettings/EditorBuildSettings.asset').read_text(encoding='utf-8-sig')))
    art_guids = set()
    for m in (ROOT/'Assets/Art').rglob('*.meta'):
        g = re.search(r'^guid: (\w+)',m.read_text(encoding='utf-8-sig'),re.M)
        if g: art_guids.add(g[1])
    count = 0
    for p in (ROOT/'Assets/Dialogue/CSV').rglob('*.csv'):
        entry = scenarios.get(norm(p.stem))
        if not entry: continue # Characters and runtime-only event CSVs are separate formats.
        asset, data, blocks = entry
        rows = list(csv.DictReader(p.open(encoding='utf-8-sig', newline='')))
        assert [r['lineId'] for r in rows] == [b.splitlines()[0].strip() for b in blocks], f'{p}: row order mismatch'
        local = {r['lineId'] for r in rows}
        for row, block in zip(rows, blocks):
            id = row['lineId']; rows_by_id[id] = row
            for field in ['text','speakerId','expressionId','nextLineId','nextScenePath']:
                m = re.search(r'^    '+field+r':[ \t]*((?:"(?:[^"\\]|\\.)*")|[^\n]*)', block, re.M | re.S)
                actual = scalar(m[1]).replace('\r\n','\n') if m else ''
                expected = (row.get(field) or '').replace('\r\n','\n')
                assert actual == expected, f'{id}: {field} differs between CSV and asset'
            choices = re.findall(r'^    - text: (.*)\n      nextLineId: (.*)', block, re.M)
            expected = [(row[k], row.get(k.replace('Text','NextLineId'),'')) for k in row
                        if re.fullmatch('choice[0-9]+Text', k) and row[k]]
            assert [(scalar(t),scalar(n)) for t,n in choices] == expected, f'{id}: choice labels/targets differ'
            for _, target in expected: assert target in local, f'{id}: choice target missing: {target}'
            dest = row.get('nextScenePath') or ''
            if dest: assert dest in enabled and (ROOT/dest).is_file(), f'{id}: disabled/missing destination {dest}'
            elif row.get('nextLineId'): assert row['nextLineId'] in local, f'{id}: next line missing'
            for match in re.finditer(r'^    background: .*guid: (\w+)',block,re.M):
                # Resolve the GUID to an actual art asset, rather than accepting any nonzero reference.
                assert match[1] in art_guids, f'{id}: missing background art'
            count += 1
    def row(id):
        assert id in rows_by_id, f'Missing production row {id}'
        return rows_by_id[id]
    def walk(start, end):
        seen=set(); id=start
        while True:
            assert id not in seen, f'Loop in result route: {id}'
            seen.add(id); r=row(id)
            if r.get('nextScenePath'):
                assert id==end and r['nextScenePath']=='Assets/Scenes/GameMap/Title.unity', f'Wrong result ending: {id}'
                return
            id=r.get('nextLineId'); assert id, f'Unexpected end at {r["lineId"]}'
    if mode=='gauge':
        assert row('chapter_kayo_5_025')['nextScenePath']=='Assets/MeterGame/Scenes/TimingMeterGame.unity'
        for outcome in ['success','failure']:
            first=row('chapter_kayo_meter_'+outcome+'_001')
            assert first['backgroundPath'], 'Meter return background missing'
        assert row('chapter_kayo_meter_success_003')['nextLineId']=='chapter_kayo_6_001'
        assert row('chapter_kayo_meter_failure_002')['nextLineId']=='chapter_kayo_6_001'
        assert row('chapter_kayo_end_004')['nextScenePath']=='Assets/Scenes/GameMap/Title.unity'
    if mode=='rescue':
        assert row('chapter_yowashi_rescue_014')['nextScenePath']=='Assets/MeterGame/Scenes/TimingMeterGame.unity'
        for start,end in [('chapter_yowashi_rescue_015','chapter_yowashi_rescue_038'),('chapter_yowashi_rescue_039','chapter_yowashi_rescue_042')]:
            assert row(start)['backgroundPath'], 'Yowashi meter return background missing'
            walk(start,end)
        for id in ['chapter_yowashi_consultation_004','chapter_yowashi_consultation_005']:
            r=row(id); targets=[v for k,v in r.items() if re.fullmatch('choice[0-9]+NextLineId',k) and v]
            assert len(targets)==3 and targets.count(id+'_wrong')==2, f'{id}: wrong evidence does not reach its retry line'
            assert row(id+'_wrong')['nextLineId']==id, f'{id}: retry line does not return to question'
    if mode=='catch':
        assert row('chapter_yowashi_beforecatch_019')['nextScenePath']=='Assets/FallingGame/Scenes/falling_verYowashi.unity'
        walk('chapter_yowashi_catchfailure_001','chapter_yowashi_catchfailure_009')
        assert row('chapter_yowashi_catchsuccess_001')['backgroundPath']
        scene=(ROOT/'Assets/FallingGame/Scenes/falling_verYowashi.unity').read_text(encoding='utf-8-sig')
        assert 'returnSceneName: NovelScene_Yowashi' in scene and 'successScoreExclusive: 3000' in scene
        ids=re.findall(r'^--- !u!\d+ &(\d+)',scene,re.M)
        assert len(ids)==len(set(ids)), 'Duplicate catch scene IDs'
        for id in re.findall(r'\{fileID: (\d+)\}',scene): assert id=='0' or id in ids, f'Dangling catch reference {id}'
    for name in ['NovelScene','NovelScene_Kayo','NovelScene_Moteru','NovelScene_Yowashi']:
        scene=(ROOT/f'Assets/Scenes/GameMap/{name}.unity').read_text(encoding='utf-8-sig')
        seq=re.search(r'^  scenario: .*?^  characterDatabase:',scene,re.M|re.S)[0]
        route_ids=[]
        for g in re.findall(r'guid: (\w+)',seq):
            assert g in guid_paths, f'{name}: missing scenario GUID {g}'
            _,_,blocks=scenarios[norm(guid_paths[g].stem)]
            route_ids += [b.splitlines()[0].strip() for b in blocks]
        assert len(route_ids)==len(set(route_ids)), f'{name}: ambiguous resume IDs'
        for target in re.findall(r'^  \w*(?:ResumeLineId|resumeLineId): (\S+)',scene,re.M):
            assert target in route_ids, f'{name}: unreachable resume row {target}'
    print(f'PASS: {mode}: {count} CSV/asset rows, choices, backgrounds, scene paths and unique route resume IDs; current result contracts checked.')

if __name__=='__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('mode',choices=['routes','gauge','rescue','catch'])
    verify(parser.parse_args().mode)
