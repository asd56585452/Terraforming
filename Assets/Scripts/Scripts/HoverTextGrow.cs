using UnityEngine;
using TMPro;
using System.Collections;

public class HoverTextGrow : MonoBehaviour
{
    public TextMeshProUGUI text;
    public float hoverScale = 1.1f;
    public float speed = 10f;

    Vector3 originalScale;

    void Start()
    {
        originalScale = text.transform.localScale;
    }

    public void OnHoverEnter()
    {
        StopAllCoroutines();
        StartCoroutine(ScaleTo(originalScale * hoverScale));
        AudioManager.instance.hover.Play();
    }

    public void OnHoverExit()
    {
        StopAllCoroutines();
        StartCoroutine(ScaleTo(originalScale));
    }

    public void OnClick()
    {
        AudioManager.instance.click.Play();
    }

    IEnumerator ScaleTo(Vector3 target)
    {
        while (Vector3.Distance(text.transform.localScale, target) > 0.001f)
        {
            text.transform.localScale = Vector3.Lerp(text.transform.localScale, target, Time.deltaTime * speed);
            yield return null;
        }
        text.transform.localScale = target;
    }
}
