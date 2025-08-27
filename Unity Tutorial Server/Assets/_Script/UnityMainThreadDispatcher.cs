using System;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static Queue<Action> executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher instance;

    void Awake()
    {
        instance = this;
    }

    public static UnityMainThreadDispatcher Instance()
    {
        return instance;
    }

    public void Enqueue(Action action)
    {
        if (action == null)
            return;

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }
}