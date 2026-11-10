using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;

public class BrewingController : MonoBehaviour
{
    public int MaxCapacity;

    [Header("Controllers")]

    [field: SerializeField]
    public CustomerController customerController { get; private set; }

    [SerializeField]
    private RatingController ratingController;

    [field: SerializeField]
    public TrackerController Tracker {  get; private set; }

    [field: SerializeField]
    public HeldObjectController HoldController { get; private set; }

    [field: SerializeField]
    public MugCabinetController CabinetController { get; private set; }

    [field: SerializeField]
    public DishwashingController DishController { get; private set; }

    [SerializeField]
    private DrinkSummaryController summaryController;

    [Space]
    [Header("Profit Controls")]
    /// <summary>
    /// Handle the profit calculations
    /// </summary>
    [SerializeField]
    private int errorValue = 3;

    [SerializeField]
    private float wantedIngredientsMultiplier = 1.2f;

    [SerializeField]
    private float unwantedIngredientsMultiplier = 0.8f;

    [SerializeField]
    private float bonusModifier = 0.6f;

    [Space]
    [Header("Probability")]
    //used to increase odds of low chance.
    [SerializeField]
    private float pityPercent;

    [SerializeField]
    private float highlyLikelyPercent = 0.8f;

    [SerializeField]
    private int timesToPityLikelySuccess = 3;

    //used to mitigate losses at high success rate. 
    private int countSinceUnlikelyFail;

    [Space]
    [Header("Timings")]

    [SerializeField]
    private float reasonDisplayTime = 0.4f;

    [SerializeField]
    private float cameraTransitionDelayTime = 0.3f;

    private bool canSubmitMug = true;
    private StateRollupData rollupData;

    private Mug _submittedMug;
    public Mug SubmittedMug
    {
        get
        {
            return _submittedMug;
        }
        set
        {
            if (_submittedMug == null && canSubmitMug)
            {
                canSubmitMug = false;
                _submittedMug = value;
            }
            ValidateSubmittedMug();
        }
    }



    private InputActions_Actions actions;

    #region Unity
    private void Awake()
    {
        actions = new InputActions_Actions();
        actions.Enable();
        actions.UI.RightClick.performed += ctx => OnCancel();

        TransitionController transition = TimeManager.instance.TransitionController;
        if (transition != null)
        {
            if (transition.IsPlayingTransition)
            {
                transition.OnTransitionComplete += PlayBrewingIntro;
            }
            else
            {
                PlayBrewingIntro(null, null);
            }

        }

    }

    private void OnDestroy()
    {
        if (actions != null)
        {
            actions.Disable();
        }

        if (customerController != null)
        {
            customerController.NextOrderReady -= OnNextOrderReady;
        }
        if (HoldController != null)
        {
            HoldController.multiCameraController.TransitionComplete -= CameraTransitionComplete;
        }
        TransitionController transition = TimeManager.instance.TransitionController;
        if (transition != null)
        {
            transition.OnTransitionComplete -= PlayBrewingIntro;
        }
    }
    #endregion
    private void OnCancel()
    {

    }

    private void Start()
    {

    }

    private void PlayBrewingIntro(object sender, EventArgs e)
    {
        TimeManager.instance.TransitionController.OnTransitionComplete -= PlayBrewingIntro;
        HoldController.multiCameraController.TransitionComplete += CameraTransitionComplete;
        StartCoroutine(TransitionCamerasIn());
    }

    private void CameraTransitionComplete(object sender, EventArgs e)
    {
        HoldController.multiCameraController.TransitionComplete -= CameraTransitionComplete;
        StartBrewing();
    }

    private IEnumerator TransitionCamerasIn()
    {
        yield return new WaitForSeconds(cameraTransitionDelayTime);
        HoldController.multiCameraController.TransitionToDefault();

    }



    private void StartBrewing()
    {
        canSubmitMug = true;
        customerController.Init();

    }

    private void ValidateSubmittedMug()
    {
        float additionalScore = 0f;
        float percent;
        float score = 1f;

        CoffeeOrder target = customerController.WaitingOrders.Dequeue();
        DrinkInfo current = SubmittedMug.FilledData;

        int countUnder; 

        //Calculate success rate
        int total = DrinkInfo.Sum(target.TargetInfo);
        int under = DrinkInfo.AmountLessThan(current, target.TargetInfo, out countUnder);
        int over = EmotionInfo.AmountGreaterThan(current.Emotions, target.TargetInfo.Emotions);

        //Modify for milk
        under = Mathf.Max(under - Mathf.Min(current.Emotions.WildCard, countUnder), 0);

        float chanceToSucceed = 1 - ((float)under / (float)Mathf.Max(total,1));

        float r = UnityEngine.Random.Range(0, 1f);
        bool success = r <= chanceToSucceed && current <= target.MaxInfo;
        percent = chanceToSucceed;

        //If we succeed calculate additional payouts
        if (success)
        {

            //handle liked / disliked ingredients 
            score = chanceToSucceed; 

        }
        else
        {
            score = 0f;
        }
        
        //pity percent
        if(score > 0 && score < pityPercent)
        {
            score = pityPercent;
        }

        //loss string at high percent mitigation
        if(!success && score >= highlyLikelyPercent)
        {
            if(countSinceUnlikelyFail < timesToPityLikelySuccess)
            {
                //pity 
                success = true;
                countSinceUnlikelyFail++;
            }
            else
            {
                countSinceUnlikelyFail = 0;
            }
        }

        //handle additial score
        additionalScore = current.Score;
        //score = score * (additionalScore);

        float newScore;
        string reason = ratingController.GetReason(current, target, score * additionalScore, out newScore, MaxCapacity);

        summaryController.FinishedSummary -= OnFinishedSummary;
        summaryController.FinishedSummary += OnFinishedSummary;

        summaryController.ShowSummary(current.Emotions, target, percent, newScore, reason);


        Debug.Log("Current info " + current);
        Debug.Log("Reason " + reason);

        //This will need to be changed once people can sit down.
        DishController.EnqueueDish(SubmittedMug.MugPrefab);

    }

    private void OnFinishedSummary(object sender, StateRollupData e)
    {
        rollupData = e;
        summaryController.FinishedSummary -= OnFinishedSummary;
        //Clean up
        _submittedMug = null;

        customerController.NextOrderReady += OnNextOrderReady;
        customerController.TransitionCustomerOut();
    }



    private void OnNextOrderReady(object sender, EventArgs e)
    {
        customerController.NextOrderReady -= OnNextOrderReady;

        StartCoroutine(WaitForTimeBetweenCustomers());
    }

    private IEnumerator WaitForTimeBetweenCustomers()
    {
        float t = UnityEngine.Random.Range(0.5f, 5f);
        yield return new WaitForSeconds(t);
        if (!customerController.CheckForNewOrder())
        {
            TimeManager timeManager = TimeManager.instance;
            RunTheCounterState slot = timeManager.GetCurrentSlot() as RunTheCounterState;
            if (slot != null)
            {

                slot.EndState(new TimeSlot.EventFinishedArgs(rollupData));
            }
        }
        
        customerController.TransitionCustomerIn();
        canSubmitMug = true;
    }


}
