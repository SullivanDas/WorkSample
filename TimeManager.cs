using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.Rendering.GPUSort;

public class TimeManager : Singleton<TimeManager>
{
    #region properties

    [field: SerializeField]
    public List<int> TimeSlotActionCount { get; private set; }

    public int PreviousStateIndex { get; private set; }

    public int CurrentTimeIndex { get; set;}

    public int CurrentSlotsUsed { get; set;}

    /// <summary>
    /// The state used for selecting which state is next
    /// </summary>
    [field: SerializeField]
    public TimeSlot SelectionState { get; private set; }

    /// <summary>
    /// The sate used for rolling up any persistant statistics. 
    /// </summary>
    [field: SerializeField]
    public TimeSlot SummaryState { get; private set; }

    [field: SerializeField]
    public TimeSlot EndDayState { get; private set; }

    /// <summary>
    /// The available states to enable
    /// </summary>
    [field: SerializeField]
    public List<TimeSlot> AvailableTimeSlots { get; private set; }

    [field: SerializeField]
    public TransitionController TransitionController { get; private set; }


    #endregion

    #region fields
    /// <summary>
    /// A dictionary of the states controlled by the state manager
    /// </summary>
    private List<string> stateKeys = new List<string>();

    /// <summary>
    /// Ref to the currentState
    /// </summary>
    private int currentStateIndex;

    #endregion

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach(TimeSlot slot in AvailableTimeSlots)
        {
            stateKeys.Add(slot.GetStateName());
        }

        CurrentTimeIndex = 0;
        CurrentSlotsUsed = 0;
        currentStateIndex = GetStateKey(SelectionState.GetStateName());

        if(SceneManager.GetActiveScene().name == "GameEntry")
            StartLoop();
    }

    /// <summary>
    /// Starts the gameplay loop
    /// </summary>
    void StartLoop()
    {
        PreviousStateIndex = -1;
        SelectionState.OnStateFinished += OnStateFinished;
        SelectionState.OnEnter();
        TransitionController.PlayTransition(TransitionController.MainScene, TransitionController.Transitions.Empty, TransitionController.Transitions.Empty);
    }

    public int GetStateKey(string stateName)
    {
        try
        {
            int key = stateKeys.IndexOf(stateName);
            return key;
        }
        catch
        {
            return -1;
        }
    }

    public TimeSlot.TimeBlocks GetCurrentBlock()
    {
        switch (CurrentTimeIndex)
        {
            case 0:
                return TimeSlot.TimeBlocks.Prep;
            case 1:
                return TimeSlot.TimeBlocks.BusinessHours;
            case 2:
                return TimeSlot.TimeBlocks.Freetime;
            default:
                return TimeSlot.TimeBlocks.None;
        }
    }

    public TimeSlot GetCurrentSlot()
    {
        return AvailableTimeSlots[currentStateIndex];
    }

    private void OnStateFinished(object sender, EventArgs e)
    {
        bool shouldAdvanceDay = false;

        AvailableTimeSlots[currentStateIndex].OnStateFinished -= OnStateFinished;

        PreviousStateIndex = currentStateIndex;
        TimeSlot.EventFinishedArgs args = (TimeSlot.EventFinishedArgs)e;
        currentStateIndex = args.NextState;
        TimeSlot nextSlot = AvailableTimeSlots[currentStateIndex];

        //check if we need to advance to the next block
        if (CurrentSlotsUsed == TimeSlotActionCount[CurrentTimeIndex])
        {
            if(CurrentTimeIndex + 1 >= TimeSlot.BlockCount)
            {
                nextSlot = AvailableTimeSlots[GetStateKey(EndDayState.GetStateName())];
                //Update our index
                currentStateIndex = GetStateKey(nextSlot.GetStateName());
                shouldAdvanceDay = true;
                if(args != null && args.RollupData != null)
                {
                    args.RollupData.ShouldAdvanceDay = true;
                }
            }
            CurrentTimeIndex = (CurrentTimeIndex + 1) % TimeSlot.BlockCount;
            CurrentSlotsUsed = 0;
            
        }



        CurrentSlotsUsed += nextSlot.SlotCost;
        nextSlot.OnStateFinished += OnStateFinished;

        //Pass any persistant data
        if (args.RollupData != null)
        {
            nextSlot.OnEnter(args.RollupData);
        }
        //We may have ended on an event that didn't increase our money
        else if (shouldAdvanceDay)
        {
            nextSlot.OnEnter(new StateRollupData(true));
        }
        else
        {

            nextSlot.OnEnter();
        }



    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }

    /// <summary>
    /// Sanity check for ensuring no memory leak from event subscribing
    /// </summary>
    private void UnregisterEvents()
    {
        foreach(var state in AvailableTimeSlots)
        {
            state.OnStateFinished -= OnStateFinished;
        }
    }
}
