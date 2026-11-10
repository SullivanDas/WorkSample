using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Mood;

public class Cauldron : MonoBehaviour, IFillable, IHoverable
{
    #region const
    private const string EMPTY = "Empty_Cauldron";
    private const string POUR = "Pour_Cauldron";

    private const string STAR1 = "Stars_1";
    private const string STAR2 = "Stars_2";
    private const string STAR3 = "Stars_3";
    private const string IDlE = "Idle";

    #endregion
    [SerializeField]
    private BrewingController brewingController;

    [SerializeField]
    private IngredientList ingredientList;

    [SerializeField]
    private MugCounter mugCounter;

    [SerializeField]
    private TimingBar timingBar;
    [SerializeField]
    private Animator starsAnimator;

    [SerializeField]
    protected PhysicalButton button;

    [SerializeField]
    private int chocolateIncreaseValue = 1;

    [SerializeField]
    private int espressoIncreaseValue = 2;

    [SerializeField]
    private float timeout;


    public bool ControlsTransform { get; set; }
    public EventHandler ForceFinishPour;

    private Ingredient nextIngredient;
    
    private DrinkInfo currentInfo;
    private List<EmotionInfo.AvailableEmotions> emotionsToReturnOnClear = new List<EmotionInfo.AvailableEmotions> ();
    private Animator animator;

    private bool isFilling;
    private bool isWaiting;
    private bool canFill;

    private float currentTimeout;


    #region Unity

    private void Awake()
    {
        timingBar.OnBarFilled += OnFilled;
    }
    private void Start()
    {
        timingBar.gameObject.SetActive (false);
        currentInfo = new DrinkInfo();
        button.ButtonComponent.onClick.RemoveAllListeners();
        button.ButtonComponent.onClick.AddListener(() => OnLadleClicked());
        animator = GetComponent<Animator>();
        isFilling = false;
        isWaiting = false;
        currentTimeout = 0;
        canFill = true;
    }

    private void OnDestroy()
    {
        if(button != null)
        {
            button.ButtonComponent.onClick.RemoveAllListeners();
        }
        if(timingBar != null)
        {
            timingBar.OnBarFilled -= OnFilled;
        }
    }

    private void LateUpdate()
    {
        if (isFilling) 
        {
            currentTimeout += Time.deltaTime;
            if ( currentTimeout > timeout)
            {
                currentTimeout = 0;
                EndFill();
            }
        }
    }
    #endregion

    #region interfaces
    public void Fill(Ingredient ingredient, float maxTime)
    {
        if (!canFill)
            return;

        if (isWaiting && isFilling)
        {
            SlamPourSuccessAnim();
        }

        //we're still filling. 
        currentTimeout = 0;

        if(!isWaiting && !isFilling && ingredient.Info.Capacity + currentInfo.Capacity <= brewingController.MaxCapacity)
        {
            timingBar.gameObject.SetActive(true);

            isFilling = true;
            //Save the ingredient for later 
            nextIngredient = ingredient;

            timingBar.StartFill(maxTime, UnityEngine.Random.Range(maxTime/2f, maxTime * 3/4));
            //timingBar.StartFill(maxTime, maxTime / 2);
        }


        
    }

    private void OnFilled(object sender, TimingBarEventArgs e)
    {

        HandlePourScore(e.score);
    }

    public void EndFill()
    {
        isFilling = false;
        if (nextIngredient == null || nextIngredient.Info.Capacity + currentInfo.Capacity > brewingController.MaxCapacity)
            return;


        int score = timingBar.EndFill();

        HandlePourScore(score);
        
    }
    #endregion

    private void HandlePourScore(int score)
    {

        currentInfo += nextIngredient.Info;
        currentInfo.Score += score;

        StopAllCoroutines();
        StartCoroutine(PlayPourSuccessAnim(score));

        brewingController.HoldController.Release();
        InsertIngredient(nextIngredient);

        isWaiting = true;
    }

    private void SlamPourSuccessAnim()
    {
        StopAllCoroutines();
        starsAnimator.Play(IDlE);
        starsAnimator.gameObject.SetActive(false);
        timingBar.gameObject.SetActive(false);
        isWaiting = false;
        isFilling = false;
    }

    private IEnumerator PlayPourSuccessAnim(int score)
    {


        string anim = "";
        switch (score)
        {
            case 0:
                anim = STAR1;
                break;
            case 1:
                anim = STAR2;
                break;
            case 2:
                anim = STAR3;
                break;
        }

        starsAnimator.gameObject.SetActive(true);

        starsAnimator.Play(anim);
        yield return null;
        float t = AnimUtils.PlayAndGetLength(starsAnimator, anim);

        yield return new WaitForSeconds(t);

        timingBar.gameObject.SetActive(false);
        isWaiting = false;
    }

    #region Debug
    public DrinkInfo GetCurrentInfo()
    {
        return currentInfo;
    }
    #endregion

    public void PreviewEmotion(EmotionInfo info)
    {
        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, currentInfo.Emotions + info, true);
        brewingController.Tracker.ShowTracker(TrackerDisplay.TrackerState.Preview, true, true);
    }
    
    public void StopPreview()
    {
        brewingController.Tracker.ShowTracker(TrackerDisplay.TrackerState.Preview, false, true);
    }



    private void OnLadleClicked()
    {
        canFill = false;
        mugCounter.Lock(true);
        float t = AnimUtils.PlayAndGetLength(animator, POUR);

    }

    //Called from OnLadleClicked at end of animation
    private void Empty()
    {

        if (mugCounter.IsOccupied)
        {
            Debug.Log("Current Info " + currentInfo);
            mugCounter.Fill(currentInfo);
            currentInfo.Clear();
            mugCounter.Lock(false);

            //We may need to delay this depending on how we handle the exit animation
            brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Current, currentInfo.Emotions, true);
        }
        else
        {
            currentInfo.Clear();
            foreach (var emotion in emotionsToReturnOnClear)
            {
                brewingController.customerController.EmotionObjectController.SendEmotionToCupboard(emotion);
            }

            mugCounter.Lock(false);
            brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Current, currentInfo.Emotions, true);
        }

        // We'll want to empty it either way so we don't get a refund for something used last round. 
        emotionsToReturnOnClear.Clear();
        ingredientList.Clear();
        canFill = true;
    }


    public void PreviewInsertIngredient(Ingredient ingredient)
    {
        brewingController.Tracker.ShowTracker(TrackerDisplay.TrackerState.Preview, true, true);
        switch (ingredient.Name)
        {
            case IngredientsInfo.AvailableIngredients.Espresso:
                PreviewInsertEspresso();
                break;
            case IngredientsInfo.AvailableIngredients.Sugar:
                PreviewInsertSugar();
                break;
            case IngredientsInfo.AvailableIngredients.Milk:
                PreviewInsertMilk();
                break;
            case IngredientsInfo.AvailableIngredients.Ice:
                PreviewInsertIce();
                break;
            case IngredientsInfo.AvailableIngredients.Chocolate:
                PreviewInsertChocolate();
                break;
            case IngredientsInfo.AvailableIngredients.Spices:
                PreviewInsertSpices(ingredient.Emotion);
                break;
            case IngredientsInfo.AvailableIngredients.HotWater:
                PreviewHotWater();
                break;
            case IngredientsInfo.AvailableIngredients.WhippedCream:
                PreviewInsertCream();
                break;
        }

    }

    #region Preview Insert Methods

    private void PreviewInsertEspresso()
    {
        EmotionInfo result = new EmotionInfo(currentInfo.Emotions);

        SingleEmotionInfo max = new SingleEmotionInfo(EmotionInfo.AvailableEmotions.None, 0);
        foreach (var emotion in currentInfo.Emotions)
        {
            SingleEmotionInfo info = emotion as SingleEmotionInfo;
            if (info != null)
            {
                max = max.Info.Value >= info.Info.Value ? max : info;
            }
        }

        result += new SingleEmotionInfo(max.Info.Emotion, espressoIncreaseValue);
        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, result, true);
    }
    private void PreviewInsertCream()
    {
        EmotionInfo result = new EmotionInfo(currentInfo.Emotions);

        int temp = currentInfo.Emotions.Fearfulness;

        result.Fearfulness = currentInfo.Emotions.Hatefullness;
        result.Hatefullness = currentInfo.Emotions.Sadness;
        result.Sadness = currentInfo.Emotions.Calmness;
        result.Calmness = currentInfo.Emotions.Braveness;
        result.Braveness = currentInfo.Emotions.Lovingness;
        result.Lovingness = currentInfo.Emotions.Happiness;
        result.Happiness = currentInfo.Emotions.Excitedness;

        result.Excitedness = temp;

        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, result, true);
    }

    private void PreviewHotWater()
    {
        EmotionInfo result = new EmotionInfo(currentInfo.Emotions);


        if (currentInfo.Emotions.Happiness > 0)
        {
            result.Happiness--;
            result.Excitedness++;
            result.Lovingness++;
        }
        if (currentInfo.Emotions.Lovingness > 0)
        {
            result.Lovingness--;
            result.Happiness++;
            result.Braveness++;
        }
        if (currentInfo.Emotions.Braveness > 0)
        {
            result.Braveness--;
            result.Lovingness++;
            result.Calmness++;
        }
        if (currentInfo.Emotions.Calmness > 0)
        {
            result.Calmness--;
            result.Braveness++;
            result.Sadness++;
        }
        if (currentInfo.Emotions.Sadness > 0)
        {
            result.Sadness--;
            result.Hatefullness++;
            result.Calmness++;
        }
        if (currentInfo.Emotions.Hatefullness > 0)
        {
            result.Hatefullness--;
            result.Sadness++;
            result.Fearfulness++;
        }
        if (currentInfo.Emotions.Fearfulness > 0)
        {
            result.Fearfulness--;
            result.Hatefullness++;
            result.Excitedness++;
        }
        if (currentInfo.Emotions.Excitedness > 0)
        {
            result.Excitedness--;
            result.Happiness++;
            result.Fearfulness++;
        }

        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, result, true);
    }

    private void PreviewInsertSpices(EmotionInfo.AvailableEmotions emotion)
    {
        EmotionInfo temp = new EmotionInfo(currentInfo.Emotions);

        temp += new SingleEmotionInfo(emotion, -999);

        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, temp, true);
    }

    private void PreviewInsertChocolate()
    {
        EmotionInfo temp = new EmotionInfo(currentInfo.Emotions);
        foreach (var emotion in currentInfo.Emotions)
        {
            SingleEmotionInfo info = emotion as SingleEmotionInfo;
            if (info != null && info.Info.Value != 0)
            {
                temp += new SingleEmotionInfo(info.Info.Emotion, chocolateIncreaseValue);
            }
        }
        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, temp, true);
    }

    private void PreviewInsertIce()
    {
        EmotionInfo result = new EmotionInfo(currentInfo.Emotions);

        EmotionInfo temp = new EmotionInfo();

        temp.Sadness = result.Sadness;
        temp.Fearfulness = result.Fearfulness;
        temp.Hatefullness = result.Hatefullness;
        temp.Excitedness = result.Excitedness;

        result.Sadness = result.Happiness;
        result.Happiness = temp.Sadness;

        result.Fearfulness = result.Braveness;
        result.Braveness = temp.Fearfulness;

        result.Hatefullness = result.Lovingness;
        result.Lovingness = temp.Hatefullness;

        result.Excitedness = result.Calmness;
        result.Calmness = temp.Excitedness;

        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, result, true);
    }

    private void PreviewInsertMilk()
    {
        EmotionInfo temp = new EmotionInfo(currentInfo.Emotions);

        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, temp, true);
    }

    private void PreviewInsertSugar()
    {
        EmotionInfo result = new EmotionInfo(currentInfo.Emotions);

        int temp = currentInfo.Emotions.Happiness;

        result.Happiness = currentInfo.Emotions.Lovingness;
        result.Lovingness = currentInfo.Emotions.Braveness;
        result.Braveness = currentInfo.Emotions.Calmness;
        result.Calmness = currentInfo.Emotions.Sadness;
        result.Sadness =  currentInfo.Emotions.Hatefullness;
        result.Hatefullness = currentInfo.Emotions.Fearfulness;
        result.Fearfulness = currentInfo.Emotions.Excitedness;

        result.Excitedness = temp;

        brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Preview, result, true);
    }
    #endregion

    private void InsertIngredient(Ingredient ingredient)
    {
        try
        {
            StopPreview();
            switch (ingredient.Name)
            {
                case IngredientsInfo.AvailableIngredients.Espresso:
                    InsertEspresso();
                    break;
                case IngredientsInfo.AvailableIngredients.Sugar:
                    InsertSugar();
                    break;
                case IngredientsInfo.AvailableIngredients.Milk:
                    InsertMilk();
                    break;
                case IngredientsInfo.AvailableIngredients.Ice:
                    InsertIce();
                    break;
                case IngredientsInfo.AvailableIngredients.Chocolate:
                    InsertChocolate();
                    break;
                case IngredientsInfo.AvailableIngredients.Spices:
                    InsertSpices(ingredient.Emotion);
                    break;
                case IngredientsInfo.AvailableIngredients.HotWater:
                    HotWater();
                    break;
                case IngredientsInfo.AvailableIngredients.WhippedCream:
                    InsertCream();
                    break;
            }

            //We've changed the ingredients update it. 
            brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Current, currentInfo.Emotions, true);
            ingredientList.AddIngredient(ingredient.Name);
        }
        catch(NullReferenceException e)
        {
            Debug.LogError(e.StackTrace);
            Debug.LogError(ingredient);
        }
        
    }

    #region Insert Methods

    /// <summary>
    /// Increases every emotion by 1
    /// </summary>
    private void InsertChocolate()
    {
        foreach (var emotion in currentInfo.Emotions)
        {
            SingleEmotionInfo info = emotion as SingleEmotionInfo;
            if (info != null && info.Info.Value != 0)
            {
                currentInfo.Emotions += new SingleEmotionInfo(info.Info.Emotion, chocolateIncreaseValue);
            }
        }

    }

    /// <summary>
    /// +2 for highest emotion
    /// </summary>
    private void InsertEspresso()
    {

        SingleEmotionInfo max = new SingleEmotionInfo(EmotionInfo.AvailableEmotions.None, 0);
        foreach (var emotion in currentInfo.Emotions)
        {
            SingleEmotionInfo info = emotion as SingleEmotionInfo;
            if (info != null)
            {
                max = max.Info.Value >= info.Info.Value ? max : info;
            }
        }
        currentInfo.Emotions += new SingleEmotionInfo(max.Info.Emotion, espressoIncreaseValue);
    }

    /// <summary>
    /// Invert Emotions
    /// </summary>
    private void InsertIce()
    {
        EmotionInfo temp = new EmotionInfo();

        temp.Sadness = currentInfo.Emotions.Sadness;
        temp.Fearfulness = currentInfo.Emotions.Fearfulness;
        temp.Hatefullness = currentInfo.Emotions.Hatefullness;
        temp.Excitedness = currentInfo.Emotions.Excitedness;

        currentInfo.Emotions.Sadness = currentInfo.Emotions.Happiness;
        currentInfo.Emotions.Happiness = temp.Sadness;

        currentInfo.Emotions.Fearfulness = currentInfo.Emotions.Braveness;
        currentInfo.Emotions.Braveness = temp.Fearfulness;

        currentInfo.Emotions.Hatefullness = currentInfo.Emotions.Lovingness;
        currentInfo.Emotions.Lovingness = temp.Hatefullness;

        currentInfo.Emotions.Excitedness = currentInfo.Emotions.Calmness;
        currentInfo.Emotions.Calmness = temp.Excitedness;
    }

    /// <summary>
    /// Reduces difficulty by one. 
    /// </summary>
    private void InsertMilk()
    {

        currentInfo.Emotions.WildCard++;
    }

    /// <summary>
    /// Removes all emotions of a given type
    /// </summary>
    private void InsertSpices(EmotionInfo.AvailableEmotions emotion)
    {
        currentInfo.Emotions += new SingleEmotionInfo(emotion, -999);
    }

    /// <summary>
    /// Rotates counter-clockwise
    /// </summary>

    private void InsertSugar()
    {

        int temp = currentInfo.Emotions.Happiness;

        currentInfo.Emotions.Happiness = currentInfo.Emotions.Lovingness;
        currentInfo.Emotions.Lovingness = currentInfo.Emotions.Braveness;
        currentInfo.Emotions.Braveness = currentInfo.Emotions.Calmness;
        currentInfo.Emotions.Calmness = currentInfo.Emotions.Sadness;
        currentInfo.Emotions.Sadness = currentInfo.Emotions.Hatefullness;
        currentInfo.Emotions.Hatefullness = currentInfo.Emotions.Fearfulness;
        currentInfo.Emotions.Fearfulness = currentInfo.Emotions.Excitedness;

        currentInfo.Emotions.Excitedness = temp;


    }

    /// <summary>
    /// Rotate clockwise
    /// </summary>
    private void InsertCream()
    {
        int temp = currentInfo.Emotions.Fearfulness;

        currentInfo.Emotions.Fearfulness = currentInfo.Emotions.Hatefullness;
        currentInfo.Emotions.Hatefullness = currentInfo.Emotions.Sadness;
        currentInfo.Emotions.Sadness = currentInfo.Emotions.Calmness;
        currentInfo.Emotions.Calmness = currentInfo.Emotions.Braveness;
        currentInfo.Emotions.Braveness = currentInfo.Emotions.Lovingness;
        currentInfo.Emotions.Lovingness = currentInfo.Emotions.Happiness;
        currentInfo.Emotions.Happiness = currentInfo.Emotions.Excitedness;

        currentInfo.Emotions.Excitedness = temp;

    }

    //Break apart emotions
    private void HotWater()
    {
        EmotionInfo emotion = new EmotionInfo(currentInfo.Emotions);

        if(currentInfo.Emotions.Happiness > 0)
        {
            emotion.Happiness--;
            emotion.Excitedness++;
            emotion.Lovingness++;
        }
        if (currentInfo.Emotions.Lovingness > 0)
        {
            emotion.Lovingness--;
            emotion.Happiness++;
            emotion.Braveness++;
        }
        if (currentInfo.Emotions.Braveness > 0)
        {
            emotion.Braveness--;
            emotion.Lovingness++;
            emotion.Calmness++;
        }
        if (currentInfo.Emotions.Calmness > 0)
        {
            emotion.Calmness--;
            emotion.Braveness++;
            emotion.Sadness++;
        }
        if (currentInfo.Emotions.Sadness > 0)
        {
            emotion.Sadness--;
            emotion.Hatefullness++;
            emotion.Calmness++;
        }
        if (currentInfo.Emotions.Hatefullness > 0)
        {
            emotion.Hatefullness--;
            emotion.Sadness++;
            emotion.Fearfulness++;
        }
        if (currentInfo.Emotions.Fearfulness > 0)
        {
            emotion.Fearfulness--;
            emotion.Hatefullness++;
            emotion.Excitedness++;
        }
        if (currentInfo.Emotions.Excitedness > 0)
        {
            emotion.Excitedness--;
            emotion.Happiness++;
            emotion.Fearfulness++;
        }

        currentInfo.Emotions = emotion;
    }



    #endregion

    #region interfaces

    public void Hover(GameObject heldObject, bool start)
    {
        
    }

    public void Remove()
    {
        
    }

    public bool Confirm(GameObject heldObject)
    {
        EmotionObject obj = heldObject.GetComponent<EmotionObject>();
        if(obj != null)
        {
            currentInfo.Emotions += obj.Emotion;
            emotionsToReturnOnClear.Add(obj.EmotionType);
            Destroy(heldObject);
            brewingController.Tracker.UpdateTracker(TrackerDisplay.TrackerState.Current, currentInfo.Emotions, true);
            return true;
        }
        else
        {
            return false;
        }
    }
    #endregion


}
