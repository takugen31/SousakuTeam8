using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[AddComponentMenu("UI/Effects/Choice Button Hover Scale")]
public sealed class ChoiceButtonHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Range(1f, 1.2f), Tooltip("カーソルを合わせたときの拡大率。")]
    private float hoverScale = 1.05f;
    [SerializeField, Min(0f), Tooltip("拡大・縮小にかかる秒数。")]
    private float transitionDuration = 0.12f;

    private Button button;
    private Vector3 originalScale;
    private bool pointerInside;
    private float progress;

    private void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData) => pointerInside = true;
    public void OnPointerExit(PointerEventData eventData) => pointerInside = false;

    private void Update()
    {
        float target = pointerInside && button != null && button.IsInteractable() ? 1f : 0f;
        progress = transitionDuration <= 0f ? target :
            Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / transitionDuration);
        transform.localScale = originalScale * EvaluateScale(progress, hoverScale);
    }

    private void OnDisable()
    {
        pointerInside = false;
        progress = 0f;
        transform.localScale = originalScale;
    }

    public static float EvaluateScale(float progress, float maximumScale)
    {
        float t = Mathf.Clamp01(progress);
        return Mathf.Lerp(1f, Mathf.Max(1f, maximumScale), t * t * (3f - 2f * t));
    }
}
