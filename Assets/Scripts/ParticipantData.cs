using UnityEngine;
using System;

/// <summary>
/// Stores participant data for the trial
/// </summary>
[Serializable]
public class ParticipantData
{
    public string participantID;
    public string date;
    public int trialNumber;

    public ParticipantData()
    {
        participantID = "";
        date = "";
        trialNumber = 1;
    }

    public ParticipantData(string id, string dateStr, int trial)
    {
        participantID = id;
        date = dateStr;
        trialNumber = trial;
    }

    public override string ToString()
    {
        return $"Participant: {participantID}, Date: {date}, Trial: {trialNumber}";
    }
}
