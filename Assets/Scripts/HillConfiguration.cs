using UnityEngine;
using System;

[Serializable]
public class HillConfiguration
{
    [Header("Hill Parameters")]
    [Tooltip("Length of the hill in meters")]
    public float length = 100f;
    
    [Tooltip("Actual gradient steepness for physics calculation (percentage: 0-100)")]
    [Range(0f, 20f)]
    public float physicsGradient = 5f;
    
    [Tooltip("Visual gradient steepness - how steep it looks (percentage: 0-100)")]
    [Range(0f, 20f)]
    public float visualGradient = 5f;
    
    [Tooltip("Smooth transition at start of hill (meters)")]
    [Range(0f, 20f)]
    public float startTransition = 5f;
    
    [Tooltip("Smooth transition at end of hill (meters). Set to 50m to have hill extend to 450m in a 500m course")]
    [Range(0f, 100f)]
    public float endTransition = 5f;

    [Header("Undulation Settings")]
    [Tooltip("Enable undulating terrain (rolling hills)")]
    public bool enableUndulations = true;

    [Tooltip("Amplitude of undulations in meters (how high/low the hills are)")]
    [Range(0f, 10f)]
    public float undulationAmplitude = 1.5f;

    [Tooltip("Frequency of undulations (how often hills occur - higher = more frequent)")]
    [Range(0.01f, 0.5f)]
    public float undulationFrequency = 0.15f;

    [Tooltip("Random seed for undulation (0 = random each time)")]
    public int undulationSeed = 0;

    public HillConfiguration()
    {
        length = 100f;
        physicsGradient = 5f;
        visualGradient = 5f;
        startTransition = 5f;
        endTransition = 5f;
        enableUndulations = true;
        undulationAmplitude = 1.5f;
        undulationFrequency = 0.15f;
        undulationSeed = 0;
    }

    public HillConfiguration(float length, float physicsGradient, float visualGradient)
    {
        this.length = length;
        this.physicsGradient = physicsGradient;
        this.visualGradient = visualGradient;
        this.startTransition = 5f;
        this.endTransition = 5f;
        this.enableUndulations = true;
        this.undulationAmplitude = 1.5f;
        this.undulationFrequency = 0.15f;
        this.undulationSeed = 0;
    }

    public HillConfiguration Clone()
    {
        return new HillConfiguration
        {
            length = this.length,
            physicsGradient = this.physicsGradient,
            visualGradient = this.visualGradient,
            startTransition = this.startTransition,
            endTransition = this.endTransition,
            enableUndulations = this.enableUndulations,
            undulationAmplitude = this.undulationAmplitude,
            undulationFrequency = this.undulationFrequency,
            undulationSeed = this.undulationSeed
        };
    }

    public override string ToString()
    {
        string undulationInfo = enableUndulations ? $", Undulation: {undulationAmplitude}m @ {undulationFrequency}" : "";
        return $"Length: {length}m, Physics: {physicsGradient}%, Visual: {visualGradient}%{undulationInfo}";
    }
}
