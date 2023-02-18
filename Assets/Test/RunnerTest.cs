using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEditor;

public class RunnerTest : MonoBehaviour
{
    [CustomEditor(typeof(RunnerTest))]
    class RunnerTestEditor : Editor
    {
        private float sliderValue;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying) return;

            var target = this.target as RunnerTest;

            if (GUILayout.Button("Play Animation"))
                target.runner.Run(target.data1);
            if (GUILayout.Button("Play Active"))
                target.runner.Run(target.data2);
            if (GUILayout.Button("Play Sound"))
                target.runner.Run(target.data3);
            if (GUILayout.Button("Play Move"))
                target.runner.Run(target.data4);
            if (GUILayout.Button("Play Rotate"))
                target.runner.Run(target.data5);
            if (GUILayout.Button("Play Scale"))
                target.runner.Run(target.data6);
            if (GUILayout.Button("Play Value"))
                target.runner.Run(target.data7);
            if (GUILayout.Button("Play Invoke"))
                target.runner.Run(target.data8);
            if (GUILayout.Button("Play Invokes"))
            {
                var delay1 = 1.11111f;
                target.runner.Run(new Runner.InvokerClipData { invoke = () => print(target.name + ":1 " + Time.frameCount + ", " + delay1) }, delay1);
                var delay2 = 1.11112f;
                target.runner.Run(new Runner.InvokerClipData { invoke = () => print(target.name + ":2 " + Time.frameCount + ", " + delay2) }, delay2);
                var delay3 = 1.11114f;
                target.runner.Run(new Runner.InvokerClipData { invoke = () => print(target.name + ":3 " + Time.frameCount + ", " + delay3) }, delay3);
                var delay4 = 1.11113f;
                target.runner.Run(new Runner.InvokerClipData { invoke = () => print(target.name + ":4 " + Time.frameCount + ", " + delay4) }, delay4);
                var delay5 = 1.11110f;
                target.runner.Run(new Runner.InvokerClipData { invoke = () => print(target.name + ":5 " + Time.frameCount + ", " + delay5) }, delay5);
                var delay6 = 1.111115f;
                target.runner.Run(new Runner.InvokerClipData { invoke = () => print(target.name + ":6 " + Time.frameCount + ", " + delay6) }, delay6);
            }
            if (GUILayout.Button("Play Group"))
            {
                var group = new Runner.GroupClipData();
                group.Add(target.data1, target.data1Delay);
                group.Add(target.data2, target.data2Delay);
                group.Add(target.data3, target.data3Delay);
                group.Add(target.data4, target.data4Delay);
                group.Add(target.data5, target.data5Delay);
                group.Add(target.data6, target.data6Delay);
                group.Add(target.data7, target.data7Delay);
                group.Add(target.data8, target.data8Delay);
                target.runner.Run(group);
            }
        }
    }

    public Runner runner;

    [SerializeField]
    public Runner.AnimationClipData data0;
    public float data1Delay = 0;
    [SerializeField]
    public Runner.AnimationClipData data1;
    public float data2Delay = 0;
    [SerializeField]
    public Runner.ActiveClipData data2;
    public float data3Delay = 0;
    [SerializeField]
    public Runner.SoundClipData data3;
    public float data4Delay = 0;
    [SerializeField]
    public Runner.MoveClipData data4;
    public float data5Delay = 0;
    [SerializeField]
    public Runner.RotateClipData data5;
    public float data6Delay = 0;
    [SerializeField]
    public Runner.ScaleClipData data6;
    public float data7Delay = 0;
    [SerializeField]
    public Runner.ValueClipData<float> data7;
    public float data8Delay = 0;
    [SerializeField]
    public Runner.InvokerClipData data8;

    private void Start()
    {
        data8.invoke = () => Debug.Log(Time.frameCount + ": data8 invoke");
        runner.Run(data0);
    }

    [ContextMenu("VerifyAllData")]
    private void VerifyAllData()
    {
        data0.Verify();
        data1.Verify();
        data2.Verify();
        data3.Verify();
        data4.Verify();
        data5.Verify();
        data6.Verify();
        data7.Verify();
    }
}