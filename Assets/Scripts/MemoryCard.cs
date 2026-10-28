using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))] // 안정성을 위해 CanvasGroup 컴포넌트 필수 요구
public class MemoryCard : MonoBehaviour
{
    [Header("UI References")]
    public Image cardBackground;
    public Image symbolImage; // TextMeshProUGUI 대신 Image 컴포넌트 사용
    public Button button;

    [Header("Colors")]
    public Color backColor = new Color(0.12f, 0.16f, 0.23f); // 뒷면 색상
    public Color frontColor = new Color(1f, 1f, 1f); // 앞면 배경 (이미지가 돋보이도록 흰색 추천)

    public Sprite cardSprite { get; private set; } // 고유 식별자로 Sprite 자체를 저장
    public bool isFlipped { get; private set; }
    public bool isMatched { get; private set; }

    private MemoryGameManager gameManager;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    public void Setup(Sprite sprite, MemoryGameManager manager)
    {
        cardSprite = sprite;
        symbolImage.sprite = sprite; // 전달받은 이미지를 UI에 적용
        gameManager = manager;
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        // 초기 상태: 뒷면
        isFlipped = false;
        isMatched = false;
        SetFace(false);
        canvasGroup.alpha = 1f;

        // 기존 리스너 초기화 후 재연결 (오브젝트 풀링 대비)
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnCardClicked);
        button.interactable = true;
    }

    private void OnCardClicked()
    {
        if (gameManager.lockBoard || isFlipped || isMatched) return;
        gameManager.CardRevealed(this);
    }

    public void Flip(bool faceUp)
    {
        isFlipped = faceUp;
        StartCoroutine(FlipRoutine(faceUp));
    }

    private IEnumerator FlipRoutine(bool faceUp)
    {
        float duration = 0.3f;
        Quaternion startRot = rectTransform.rotation;
        Quaternion midRot = Quaternion.Euler(0, 90, 0);
        Quaternion endRot = Quaternion.Euler(0, faceUp ? 180 : 0, 0);

        // 1. 0도 -> 90도 (반 바퀴)
        float elapsed = 0f;
        while (elapsed < duration)
        {
            rectTransform.rotation = Quaternion.Lerp(startRot, midRot, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 시점이 90도일 때 이미지와 색상을 앞면/뒷면으로 교체
        SetFace(faceUp);

        // 2. 90도 -> 180도 (나머지 반 바퀴)
        elapsed = 0f;
        while (elapsed < duration)
        {
            rectTransform.rotation = Quaternion.Lerp(midRot, endRot, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rectTransform.rotation = endRot;
    }

    private void SetFace(bool faceUp)
    {
        cardBackground.color = faceUp ? frontColor : backColor;
        symbolImage.gameObject.SetActive(faceUp);

        // 카드(부모)가 180도 회전하므로, 이미지(자식)도 Y축 180도 추가 회전하여 360도로 렌더링되게 만듦 (좌우반전 방지)
        if (faceUp)
        {
            symbolImage.rectTransform.localRotation = Quaternion.Euler(0, 180, 0);
        }
    }

    public void MatchAndFade()
    {
        isMatched = true;
        button.interactable = false;
        StartCoroutine(BounceAndFadeRoutine());
    }

    private IEnumerator BounceAndFadeRoutine()
    {
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 제자리에서 서서히 투명해지도록 alpha 값만 조절
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        canvasGroup.alpha = 0f; // 완전 투명화

        // 투명해진 상태에서 물리적인 자리(Grid Layout)는 유지하되, 클릭만 차단
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}