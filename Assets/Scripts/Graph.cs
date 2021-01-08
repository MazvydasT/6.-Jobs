using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Graph : MonoBehaviour
{
    [SerializeField]
    Transform pointPrefab;

    [SerializeField, Range(10, 200)]
    int resolution = 10;

    [SerializeField]
    FunctionLibrary.FunctionName function = default;

    public enum TransitionMode { Cycle, Random }

    [SerializeField]
    TransitionMode transitionMode = TransitionMode.Cycle;

    [SerializeField, Min(0f)]
    float functionDuration = 1f, transitionDuration = 1f;

    float duration;

    bool transitioning;

    FunctionLibrary.FunctionName transitionFunction;

    Transform[] points;

    private void Awake()
    {
        var step = 2f / resolution;
        var scale = Vector3.one * step;

        points = new Transform[resolution * resolution];

        for (int i = 0; i < points.Length; ++i)
        {
            var point = Instantiate(pointPrefab);
            point.localScale = scale;

            point.SetParent(transform, false);

            points[i] = point;
        }
    }

    void Update()
    {
        duration += Time.deltaTime;

        if (transitioning)
        {
            if (duration >= functionDuration)
            {
                duration = -functionDuration;

                transitioning = false;
            }
        }

        else
        {
            if (duration >= functionDuration)
            {
                duration = -functionDuration;

                transitioning = true;
                transitionFunction = function;

                function = transitionMode == TransitionMode.Cycle ?
                    FunctionLibrary.GetNextFunctionName(function) :
                    FunctionLibrary.GetRandomFunctionName(function);
            }
        }

        if (transitioning)
            UpdateFunctionTransition();

        else
            UpdateFunction();
    }

    void UpdateFunction()
    {
        var f = FunctionLibrary.GetFunction(function);

        var time = Time.time;
        var step = 2f / resolution;

        var v = 0.5f * step - 1f;

        for (int i = 0, x = 0, z = 0; i < points.Length; ++i, ++x)
        {
            if (x == resolution)
            {
                x = 0;
                ++z;
                v = (z + 0.5f) * step - 1f;
            }

            var u = (x + 0.5f) * step - 1f;

            points[i].localPosition = f(u, v, time);
        }
    }
    
    void UpdateFunctionTransition()
    {
        var from = FunctionLibrary.GetFunction(transitionFunction);
        var to = FunctionLibrary.GetFunction(function);

        var progress = duration / transitionDuration;

        var time = Time.time;
        var step = 2f / resolution;

        var v = 0.5f * step - 1f;

        for (int i = 0, x = 0, z = 0; i < points.Length; ++i, ++x)
        {
            if (x == resolution)
            {
                x = 0;
                ++z;
                v = (z + 0.5f) * step - 1f;
            }

            var u = (x + 0.5f) * step - 1f;

            points[i].localPosition = FunctionLibrary.Morph(u, v, time, from, to, progress);
        }
    }
}
