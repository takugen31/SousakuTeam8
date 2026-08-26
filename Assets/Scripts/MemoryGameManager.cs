using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MemoryGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public GameObject cardPrefab;
    public Transform boardGridParent;
    public GridLayoutGroup boardLayout;

    [Header("Card Images (커스텀 이미지)")]
    [Tooltip("유니티 인스펙터에서 16개의 카드 앞면 이미지를 넣어주세요.")]
    public Sprite[] cardSprites;

    [Header("UI Elements")]
    public TextMeshProUGUI stageText;
    public TextMeshProUGUI movesText;
    public TextMeshProUGUI matchesText;
    public GameObject winModal;
    public TextMeshProUGUI finalMovesText;
    public GameObject clearTextOverlay;

    [HideInInspector] public bool lockBoard = false;

    private int currentStage = 1;

    [Header("Stage Settings")]
    [Tooltip("현재 준비된 이미지(8장)에 맞춰 최대 스테이지를 2로 제한합니다. 추후 3, 4로 늘릴 수 있습니다.")]
    public int maxStage = 2;

    // 각 스테이지별 [쌍의 개수] 및 [PC 가로 배치 열(Column) 개수]
    // 추후 확장을 대비해 데이터는 4스테이지까지 모두 보존해 둡니다.
    private int[] pairsPerStage = { 4, 8, 12, 16 };
    private int[] colsPerStage = { 4, 4, 6, 8 };

    private List<MemoryCard> cards = new List<MemoryCard>();
    private MemoryCard firstCard;
    private MemoryCard secondCard;
    private int moves = 0;
    private int matches = 0;

    void Start()
    {
        InitializeGame(true);
    }

    public void InitializeGame(bool resetAll = true)
    {
        StopAllCoroutines();

        if (resetAll)
        {
            currentStage = 1;
            moves = 0;
        }

        // 기존 카드 모두 파괴
        foreach (Transform child in boardGridParent)
        {
            Destroy(child.gameObject);
        }
        cards.Clear();

        matches = 0;
        UpdateUI();

        if (winModal != null) winModal.SetActive(false);
        if (clearTextOverlay != null) clearTextOverlay.SetActive(false);

        CreateBoard();
        StartCoroutine(PreviewSequence());
    }

    private void CreateBoard()
    {
        int currentPairs = pairsPerStage[currentStage - 1];

        // PC 환경을 위한 GridLayoutGroup 열 개수 동적 변경
        if (boardLayout != null)
        {
            boardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            boardLayout.constraintCount = colsPerStage[currentStage - 1];
        }

        // 최적화: 매번 똑같은 그림이 나오지 않도록 할당된 이미지를 먼저 섞음
        List<Sprite> availableSprites = new List<Sprite>(cardSprites);
        for (int i = availableSprites.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Sprite temp = availableSprites[i];
            availableSprites[i] = availableSprites[j];
            availableSprites[j] = temp;
        }

        // 섞인 그림 중 현재 스테이지에 필요한 만큼만 추출하여 덱 생성
        List<Sprite> deck = new List<Sprite>();
        for (int i = 0; i < currentPairs; i++)
        {
            deck.Add(availableSprites[i]); // 쌍(Pair)이므로 두 번씩 추가
            deck.Add(availableSprites[i]);
        }

        // 생성된 덱을 Fisher-Yates 알고리즘으로 무작위 셔플
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Sprite temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }

        foreach (Sprite sprite in deck)
        {
            GameObject cardObj = Instantiate(cardPrefab, boardGridParent);
            MemoryCard card = cardObj.GetComponent<MemoryCard>();
            card.Setup(sprite, this);
            cards.Add(card);
        }
    }

    private IEnumerator PreviewSequence()
    {
        lockBoard = true;
        yield return new WaitForSeconds(0.1f); // 렌더링 대기

        // 모든 카드 앞면 오픈
        foreach (MemoryCard card in cards)
        {
            card.Flip(true);
        }

        yield return new WaitForSeconds(5.0f); // 5초 암기 시간

        // 모든 카드 뒷면 닫기
        foreach (MemoryCard card in cards)
        {
            card.Flip(false);
        }

        // 카드가 닫히는 애니메이션 시간(0.6초) 대기 후 클릭 허용
        yield return new WaitForSeconds(0.6f);
        lockBoard = false;
    }

    public void CardRevealed(MemoryCard card)
    {
        card.Flip(true);

        if (firstCard == null)
        {
            firstCard = card;
        }
        else
        {
            secondCard = card;
            moves++;
            UpdateUI();
            StartCoroutine(CheckMatchRoutine());
        }
    }

    private IEnumerator CheckMatchRoutine()
    {
        lockBoard = true;

        yield return new WaitForSeconds(0.6f); // 카드가 완전히 뒤집힐 때까지 대기

        // 커스텀 이미지(Sprite)가 동일한지 비교
        if (firstCard.cardSprite == secondCard.cardSprite)
        {
            // 매칭 성공 처리
            firstCard.MatchAndFade();
            secondCard.MatchAndFade();

            matches++;
            UpdateUI();

            yield return new WaitForSeconds(0.8f); // 바운스 애니메이션 대기

            int currentPairs = pairsPerStage[currentStage - 1];

            // 스테이지 클리어 확인
            if (matches == currentPairs)
            {
                if (currentStage < maxStage)
                {
                    currentStage++;
                    yield return new WaitForSeconds(1.0f); // 템포 조절용 딜레이
                    InitializeGame(false); // 시도 횟수(moves)를 유지한 채 다음 스테이지 진입
                }
                else
                {
                    StartCoroutine(ClearSequence());
                }
            }
            else
            {
                ResetBoardState();
            }
        }
        else
        {
            // 불일치 시 1초간 보여준 뒤 복구
            yield return new WaitForSeconds(1.0f);

            firstCard.Flip(false);
            secondCard.Flip(false);

            yield return new WaitForSeconds(0.6f);
            ResetBoardState();
        }
    }

    private void ResetBoardState()
    {
        firstCard = null;
        secondCard = null;
        lockBoard = false;
    }

    private void UpdateUI()
    {
        // 영문 기반의 깔끔한 UI 텍스트 출력 적용
        if (stageText != null) stageText.text = $"STAGE {currentStage} / {maxStage}";
        if (movesText != null) movesText.text = $"MOVES: {moves}";

        int currentPairs = pairsPerStage[currentStage - 1];
        if (matchesText != null) matchesText.text = $"MATCHES: {matches} / {currentPairs}";
    }

    private IEnumerator ClearSequence()
    {
        // 외부 플러그인(DOTween) 없이 자체 코루틴으로 CLEAR 텍스트 팝업 연출
        if (clearTextOverlay != null)
        {
            clearTextOverlay.SetActive(true);
            RectTransform rect = clearTextOverlay.GetComponent<RectTransform>();
            CanvasGroup cg = clearTextOverlay.GetComponent<CanvasGroup>();
            if (cg == null) cg = clearTextOverlay.AddComponent<CanvasGroup>();

            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);

                if (rect != null) rect.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.one, easeOut);
                cg.alpha = Mathf.Lerp(0f, 1f, easeOut);

                elapsed += Time.deltaTime;
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
            cg.alpha = 1f;
        }

        // 여운을 주는 2초 딜레이
        yield return new WaitForSeconds(2.0f);

        if (finalMovesText != null) finalMovesText.text = moves.ToString();
        if (winModal != null) winModal.SetActive(true);
    }
}