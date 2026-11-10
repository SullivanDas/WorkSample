using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TimingBar : MonoBehaviour
{

    [field: SerializeField, Range(0,1)]
    public float GreenErrorMargin { get; set; } = 0.1f;

    [field: SerializeField, Range(0,1)]
    public float YellowErrorMargin { get; set; } = 0.1f;

    [SerializeField]
    private float errorError = 0.002f;

    [field: SerializeField]
    public float TimeToFill { get; set; } = 1f;

    [field: SerializeField]
    public float NormalizedTargetTime { get; set; } = 0.8f;

    [SerializeField]
    private bool EndOnMaxFill;
    [SerializeField]
    private Vector2 FillDir;

    //could help to have a wrapper if we create different kinds of timing bars
    [SerializeField]
    private RectTransform TargetGraphic;
    [SerializeField]
    private RectTransform YellowTargetGraphic;
    [SerializeField]
    private RectTransform GreenTargetGraphic;

    [SerializeField]
    private Slider slider;
    private RectTransform sliderTransform;

    [Space]
    [Header("Settings")]
    [SerializeField]
    private AnimationCurve sliderCurve;

    private float currentTime = 0f;
    private float percent = 0;


    public EventHandler<TimingBarEventArgs> OnBarFilled;

    private void Awake()
    {
        currentTime = 0f;
        slider.value = 0;
        sliderTransform = slider.GetComponent<RectTransform>();


    }

    public void StartFill(float fillTime, float targetTime)
    {
        TimeToFill = fillTime;
        NormalizedTargetTime = Mathf.Min(targetTime, fillTime) / fillTime;

        SetHeightsFromTime(fillTime);
        SetTargetPositionFromNormalized(NormalizedTargetTime);

        currentTime = 0f;
        StartCoroutine(FillBar());

    }

    private void SetHeightsFromTime(float fillTime)
    {
        float sizeGreen = sliderTransform.rect.height  * GreenErrorMargin;
        float sizeYellow = sliderTransform.rect.height * YellowErrorMargin + sizeGreen;

        YellowTargetGraphic.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sizeYellow);
        GreenTargetGraphic.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sizeGreen);
    }

    //returns the score
    public int EndFill()
    {
        StopAllCoroutines();

        float result = NormalizedTargetTime - percent + errorError;
        if(Mathf.Abs(result) * 2 < GreenErrorMargin)
        {
            return 2;
        }
        else if(Mathf.Abs(result) * 2 < (YellowErrorMargin + GreenErrorMargin))
        {
            return 1;
        }
        else
        {
            return 0;
        }
        
    }

    private IEnumerator FillBar()
    {
        while(currentTime < 1)
        {
            currentTime += Time.deltaTime / TimeToFill;
            percent = sliderCurve.Evaluate(currentTime);

            slider.value = percent;
            yield return null;
        }
        float result = NormalizedTargetTime - percent + errorError;
        int score = 0;

        if (Mathf.Abs(result) * 2 < GreenErrorMargin)
        {
            score = 2;
        }
        else if (Mathf.Abs(result) * 2< (GreenErrorMargin + YellowErrorMargin))
        {
            score = 1;
        }
        else
        {
            score = 0;
        }

        if (EndOnMaxFill)
        {
            OnBarFilled?.Invoke(this, new TimingBarEventArgs(score));
        }


    }
    private void SetTargetPositionFromNormalized(float normalizedPosition)
    {

        //float x = (sliderTransform.rect.width - YellowTargetGraphic.rect.width/2) * FillDir.x * normalizedPosition;
        //float y = (sliderTransform.rect.height - YellowTargetGraphic.rect.height/2) * FillDir.y * normalizedPosition;
        float x = (sliderTransform.rect.width) * FillDir.x * normalizedPosition;
        float y = (sliderTransform.rect.height) * FillDir.y * normalizedPosition;

        YellowTargetGraphic.anchoredPosition = new Vector2(x, y);
        //float gx = (sliderTransform.rect.width - GreenTargetGraphic.rect.width / 2) * FillDir.x * normalizedPosition;
        //float gy = (sliderTransform.rect.height - GreenTargetGraphic.rect.height / 2) * FillDir.y * normalizedPosition;
        float gx = (sliderTransform.rect.width) * FillDir.x * normalizedPosition;
        float gy = (sliderTransform.rect.height) * FillDir.y * normalizedPosition;
        GreenTargetGraphic.anchoredPosition = new Vector2(gx, gy);
    }

    
}

public class TimingBarEventArgs : EventArgs
{
    public int score;
    public TimingBarEventArgs(int score)
    {
        this.score = score;
    }
}
