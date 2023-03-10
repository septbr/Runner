using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.Audio;

public class Runner : MonoBehaviour
{
    [Min(0f)]
    public float Speed = 1;
    private RunnerDirector director;

    private void Awake() => director = RunnerDirector.Create(name);
    private void Update() => director.graph.Evaluate(Time.deltaTime * Speed);
    private void OnDestroy() => director.Destroy();

    public RunnerClip Run(float delay, RunnerClipData data) => director.Run(delay, data);
    public RunnerClip Animation(float delay, Animator animator, UnityEngine.AnimationClip animation, float from = 0, float to = float.PositiveInfinity)
    {
        var data = new AnimationClipData
        {
            animator = animator,
            animation = animation,
            from = from,
            to = to,
        };
        return Run(delay, data);
    }
    public RunnerClip Active(float delay, UnityEngine.Object target, float duration)
    {
        var data = new ActiveClipData
        {
            target = target,
            duration = duration,
        };
        return Run(delay, data);
    }
    public RunnerClip Audio(float delay, AudioSource source, UnityEngine.AudioClip clip, float from = 0, float to = float.PositiveInfinity)
    {
        var data = new AudioClipData
        {
            audioSource = source,
            audioClip = clip,
            from = from,
            to = to,
        };
        return Run(delay, data);
    }
    public RunnerClip Move(float delay, Transform target, Vector3[] path, float duration)
    {
        var data = new MoveClipData
        {
            target = target,
            duration = duration,
        };
        foreach (var position in path)
            data.path.Add(new MoveClipData.Place { position = position });
        return Run(delay, data);
    }
    public RunnerClip Rotate(float delay, Transform target, Vector3[] path, float duration)
    {
        var data = new RotateClipData
        {
            target = target,
            path = new List<Vector3>(path),
            duration = duration,
        };
        return Run(delay, data);
    }
    public RunnerClip Scale(float delay, Transform target, Vector3[] path, float duration)
    {
        var data = new ScaleClipData
        {
            target = target,
            path = new List<Vector3>(path),
            duration = duration,
        };
        return Run(delay, data);
    }
    public RunnerClip Value<T>(float delay, T from, T to, Action<T> setter, float duration) where T : struct
    {
        var data = new ValueClipData<T>
        {
            from = from,
            to = to,
            setter = setter,
            duration = duration,
        };
        return Run(delay, data);
    }
    public RunnerClip Invoke(float delay, Action invoke, float duration = 0)
    {
        var data = new InvokeClipData
        {
            invoke = invoke,
            duration = duration,
        };
        return Run(delay, data);
    }

    public bool ContainsAnimationState(Animator animator, string state, params string[] states) => director.graph.ContainsState<AnimationClip>(animator, state, states);
    public bool ContainsAnimationState(Animator animator, UnityEngine.AnimationClip state, params UnityEngine.AnimationClip[] states) => director.graph.ContainsState<AnimationClip>(animator, state, states);
    public bool ContainsAudioState(AudioSource audioSource, string state, params string[] states) => director.graph.ContainsState<AudioClip>(audioSource, state, states);
    public bool ContainsAudioState(AudioSource audioSource, UnityEngine.AudioClip state, params UnityEngine.AudioClip[] states) => director.graph.ContainsState<AudioClip>(audioSource, state, states);

    public class RunnerGraph
    {
        public readonly PlayableGraph playableGraph;
        private RunnerGraph(string name) => playableGraph = PlayableGraph.Create(name);
        public static RunnerGraph Create(string name) => new RunnerGraph(name);

        public void Evaluate(float time)
        {
            playableGraph.Evaluate(time);
            CleanImmediate();
        }
        public void Destroy()
        {
            CleanImmediate();
            playableGraph.Destroy();
        }

        private List<Tuple<float, Action>> invokers = new List<Tuple<float, Action>>();
        public void Invoke(float progress, Action invoke)
        {
            var pos = invokers.Count;
            for (var index = 0; index < invokers.Count; index++)
            {
                if (progress < invokers[index].Item1)
                {
                    pos = index;
                    break;
                }
            }
            invokers.Insert(pos, new Tuple<float, Action>(progress, invoke));
        }
        public void InvokeClear() => invokers.Clear();
        public void InvokeImmediate()
        {
            foreach (var invoker in invokers)
            {
                try
                {
                    invoker.Item2();
                }
                catch (Exception e)
                {
                    Debug.LogError(Time.frameCount + ": " + e);
                }
            }
            invokers.Clear();
        }

        private List<Action> cleanups = new List<Action>();
        public void Clean(Action cleanup)
        {
            if (!cleanups.Contains(cleanup))
                cleanups.Add(cleanup);
        }
        public void CleanClear() => invokers.Clear();
        public void CleanImmediate()
        {
            foreach (var cleanup in cleanups)
                cleanup();
            cleanups.Clear();

            CleanPlayablesImmediate();
        }

        private List<RunnerPlayable> playables = new List<RunnerPlayable>();
        public void CleanPlayablesImmediate()
        {
            for (int index = 0; index < playables.Count; index++)
            {
                var playable = playables[index];
                if (!playable.IsValid())
                {
                    playable.DestroyImmediate();
                    playables.RemoveAt(index--);
                }
            }
        }
        public bool ContainsState<T>(UnityEngine.Object target, string state, params string[] states) where T : RunnerClip, IMixRunnerClip
        {
            foreach (var playable in playables)
            {
                if (playable.target == target && playable is RunnerPlayable<T> && playable.ContainsState(state, states))
                    return true;
            }
            return false;
        }
        public bool ContainsState<T>(UnityEngine.Object target, UnityEngine.Object state, params UnityEngine.Object[] states) where T : RunnerClip, IMixRunnerClip
        {
            foreach (var playable in playables)
            {
                if (playable.target == target && playable is RunnerPlayable<T> && playable.ContainsState(state, states))
                    return true;
            }
            return false;
        }

        public abstract class RunnerPlayable
        {
            public RunnerGraph graph { get; private set; }
            public UnityEngine.Object target { get; private set; }
            protected PlayableOutput output;
            protected Playable mixer;

            public bool IsValid() => target && mixer.IsValid();
            public abstract bool ContainsState(string state, params string[] states);
            public abstract bool ContainsState(UnityEngine.Object state, params UnityEngine.Object[] states);
            public void DestroyImmediate()
            {
                if (mixer.IsValid())
                {
                    for (int index = 0, inputCount = mixer.GetInputCount(); index < inputCount; index++)
                    {
                        var input = mixer.GetInput(index);
                        if (input.IsValid())
                            graph.playableGraph.DestroyPlayable(input);
                    }
                    graph.playableGraph.DestroyPlayable(mixer);
                    graph.playableGraph.DestroyOutput(output);
                }
            }

            public static T Create<T>(RunnerGraph graph, UnityEngine.Object target, Action<T> connect) where T : RunnerPlayable, new()
            {
                T playable = null;
                foreach (var p in graph.playables)
                {
                    if (p is T tp && p.target == target)
                    {
                        playable = tp;
                        break;
                    }
                }
                if (playable == null)
                {
                    playable = new T();
                    playable.graph = graph;
                    playable.target = target;

                    connect(playable);
                    graph.playables.Add(playable);
                }
                return playable;
            }
        }
    }

    private class RunnerDirector : PlayableBehaviour
    {
        public RunnerGraph graph { get; private set; }
        private List<Node> nodes = new List<Node>();

        public static RunnerDirector Create(string name)
        {
            var graph = RunnerGraph.Create(name);
            graph.playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = ScriptPlayableOutput.Create(graph.playableGraph, "RunnerDirectorOutput");
            var playable = ScriptPlayable<RunnerDirector>.Create(graph.playableGraph);
            output.SetSourcePlayable(playable);
            var director = playable.GetBehaviour();
            director.graph = graph;

            return director;
        }

        public RunnerClip Run(float delay, RunnerClipData data)
        {
            if (!data.Verify())
            {
                Debug.LogError(Time.frameCount + ": No valid clip data provided.");
                return null;
            }

            var clip = data.CreateClip();
            nodes.Add(new Node { delay = Mathf.Max(delay, 0), clip = clip });
            return clip;
        }

        public void Destroy()
        {
            foreach (var node in nodes)
                node.clip.Destroy();
            nodes.Clear();
            nodes = null;

            graph.Destroy();
            graph = null;
        }

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            foreach (var node in nodes)
            {
                float deltaTime = info.deltaTime;
                float progress = 0;
                if (node.delay > 0)
                {
                    node.delay -= deltaTime;

                    if (node.delay <= 0)
                    {
                        deltaTime = -node.delay;
                        progress = (info.deltaTime - deltaTime) / info.deltaTime;
                    }
                }
                if (node.delay <= 0)
                    FrameScheduler.Running(node.clip, graph, deltaTime, progress);
            }
        }
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            graph.InvokeImmediate();
            for (var index = 0; index < nodes.Count; index++)
            {
                var clip = nodes[index].clip;
                if (clip.DestroyIf(RunnerClip.RunState.Done))
                    nodes.RemoveAt(index--);
            }
        }

        private class Node { public float delay; public RunnerClip clip; }
        private class FrameScheduler : RunnerClip
        {
            private FrameScheduler(RunnerClipData data) : base(data) => throw new NotImplementedException();
            public static void Running(RunnerClip clip, RunnerGraph graph, float deltaTime, float progress)
                => Scheduler.Running(clip, graph, deltaTime, progress);
        }
    }

    public delegate float EaseFunction(float t);
    public static class Ease
    {
        public static readonly EaseFunction Linear = t => t;
        public static readonly EaseFunction InSine = t => 0f - (float)Math.Cos((double)(t * 1.57079637f)) + 1f;
        public static readonly EaseFunction OutSine = t => (float)Math.Sin((double)(t * 1.57079637f));
        public static readonly EaseFunction InOutSine = t => -0.5f * ((float)Math.Cos((double)(3.14159274f * t)) - 1f);
        public static readonly EaseFunction InQuad = t => t * t;
        public static readonly EaseFunction OutQuad = t => (0f - t) * (t - 2f);
        public static readonly EaseFunction InOutQuad = t => (t *= 2f) < 1f ? 0.5f * t * t : -0.5f * ((t -= 1f) * (t - 2f) - 1f);
        public static readonly EaseFunction InCubic = t => t * t * t;
        public static readonly EaseFunction OutCubic = t => (t = t - 1f) * t * t + 1f;
        public static readonly EaseFunction InOutCubic = t => (t *= 2f) < 1f ? 0.5f * t * t * t : 0.5f * ((t -= 2f) * t * t + 2f);
        public static readonly EaseFunction InQuart = t => t * t * t * t;
        public static readonly EaseFunction OutQuart = t => 0f - ((t = t - 1f) * t * t * t - 1f);
        public static readonly EaseFunction InOutQuart = t => (t *= 2f) < 1f ? 0.5f * t * t * t * t : -0.5f * ((t -= 2f) * t * t * t - 2f);
        public static readonly EaseFunction InQuint = t => t * t * t * t * t;
        public static readonly EaseFunction OutQuint = t => (t = t - 1f) * t * t * t * t + 1f;
        public static readonly EaseFunction InOutQuint = t => (t *= 2f) < 1f ? 0.5f * t * t * t * t * t : 0.5f * ((t -= 2f) * t * t * t * t + 2f);
        public static readonly EaseFunction InExpo = t => t != 0f ? (float)Math.Pow(2.0, (double)(10f * (t - 1f))) : 0f;
        public static readonly EaseFunction OutExpo = t => t == 1f ? 1f : 0f - (float)Math.Pow(2.0, (double)(-10f * t)) + 1f;
        public static readonly EaseFunction InOutExpo = t => (t == 0f || t == 1f) ? t : ((t *= 2f) < 1f ? 0.5f * (float)Math.Pow(2.0, (double)(10f * (t - 1f))) : 0.5f * (0f - (float)Math.Pow(2.0, (double)(-10f * (t -= 1f))) + 2f));
        public static readonly EaseFunction InCirc = t => 0f - ((float)Math.Sqrt((double)(1f - t * t)) - 1f);
        public static readonly EaseFunction OutCirc = t => (float)Math.Sqrt((double)(1f - (t = t - 1f) * t));
        public static readonly EaseFunction InOutCirc = t => (t *= 2f) < 1f ? -0.5f * ((float)Math.Sqrt((double)(1f - t * t)) - 1f) : 0.5f * ((float)Math.Sqrt((double)(1f - (t -= 2f) * t)) + 1f);
    }

    public const float MinWeight = -1000, MaxWeight = 1000;

    [Serializable]
    public class RunnerClipData
    {
        [Min(0)]
        public float speed = 1;
        [Min(0)]
        public float duration = 0;

        public virtual bool Verify()
        {
            speed = speed <= 0 ? 1 : speed;
            duration = MathF.Max(0, duration);
            return true;
        }
        public virtual RunnerClip CreateClip() => new RunnerClip(this);
    }

    private abstract class RunnerPlayable<T> : RunnerGraph.RunnerPlayable where T : RunnerClip, IMixRunnerClip
    {
        protected List<PlayableState> states = new List<PlayableState>();
        protected PlayableState CreateState(Playable playable, T clip, float time = 0, float weight = 0)
        {
            var state = new PlayableState(playable, clip, time);
            state.playable.Pause();
            mixer.AddInput(state.playable, 0, state.weight);
            SetTime(state, state.time);
            states.Add(state);
            return state;
        }

        private void UpdateWeightTo()
        {
            bool hasRightLimit = false, hasRightCount = false, hasUpLeftLimit = false;
            foreach(var state in states)
            {
                var weightTo = state.clip.Exiting || state.clip.State != RunnerClip.RunState.Running ? 0 : state.clip.Weight;
                if (weightTo != 0)
                {
                    if (weightTo >= MaxWeight)
                    {
                        hasRightLimit = true;
                        break;
                    }
                    if (weightTo > 0)
                        hasRightCount = true;
                    else if (weightTo > MinWeight)
                        hasUpLeftLimit = true;
                }
            }
            foreach(var state in states)
            {
                var weightTo = state.clip.Exiting || state.clip.State != RunnerClip.RunState.Running ? 0 : state.clip.Weight;
                if (weightTo != 0)
                {
                    if (hasRightLimit)
                        weightTo = weightTo >= MaxWeight ? 1 : 0;
                    else if (hasRightCount)
                        weightTo = weightTo > 0 ? weightTo : 0;
                    else if (hasUpLeftLimit)
                        weightTo = weightTo <= MinWeight ? 0 : weightTo + MaxWeight;
                    else
                        weightTo = 1;
                }
                state.weightTo = weightTo;
            }
        }
        /// <summary>
        /// ??????????????????0, ??????????????????0, ????????????????????????, ??????????????????
        /// ?????????????????????>0???, <0??????????????????????????????0;
        /// ????????????????????????1000???, ??????<1000??????????????????0;
        /// ????????????????????????-1000???, ???????????????0;
        /// ?????????????????????????????????????????????????????????, ?????????????????????1???
        /// ??????????????????????????????1s, ??????????????????????????????, ????????????
        /// </summary>
        private void UpdateWeight(float deltaTime)
        {
            float weightTotal = 0;
            foreach(var state in states)
            {
                var weight = state.weightTo;
                if (state.clip.Transition > 0 && state.weight != state.weightTo && state.transition == -1)
                {
                    state.transition = 0;
                    state.weightFrom = state.weight;
                }
                if (state.transition != -1)
                {
                    state.transition += deltaTime;
                    weight = Mathf.Lerp(state.weightFrom, state.weightTo, state.transition / state.clip.Transition);
                    if (state.transition >= state.clip.Transition) state.transition = -1;
                }

                weightTotal += weight;
                state.weight = weight;
            }

            for (var index = 0; index < states.Count; index++)
            {
                var weight = weightTotal > 0 ? states[index].weight / weightTotal : 0f;
                mixer.SetInputWeight(index, weight);
                // Debug.Log(Time.frameCount + ": SetInputWeight: " + index + ", " + weight + ", " + states[index].weight + ", " + states[index].weightTo);
            }
        }
        protected virtual void SetTime(PlayableState state, float time) => state.playable.SetTime(time);
        public void SetTime(IMixRunnerClip runnerClip, float time, bool playableStateChanged, bool exitingChanged)
        {
            foreach (var state in states)
            {
                if (state.clip == runnerClip)
                {
                    var lastTime = state.time;
                    state.time = time;
                    SetTime(state, time);

                    var desiredWeightDirty = playableStateChanged || exitingChanged;
                    if (desiredWeightDirty)
                        UpdateWeightTo();

                    if (desiredWeightDirty || state.transition != -1)
                    {
                        var deltaTime = time - lastTime;
                        if (deltaTime < 0) deltaTime = time + runnerClip.To - lastTime;
                        UpdateWeight(deltaTime);
                    }
                    return;
                }
            }
        }

        protected void DestroyImmediate(IMixRunnerClip runnerClip)
        {
            for (var index = 0; index < states.Count; index++)
            {
                var state = states[index];
                if (state.clip == runnerClip)
                {
                    states.RemoveAt(index);
                    graph.playableGraph.DestroyPlayable(state.playable);

                    for (; index < states.Count; index++)
                    {
                        mixer.DisconnectInput(index + 1);
                        mixer.ConnectInput(index, states[index].playable, 0);
                    }
                    mixer.SetInputCount(states.Count);
                    UpdateWeightTo();
                    UpdateWeight(0);
                    return;
                }
            }
        }
        public void Destroy(IMixRunnerClip runnerClip) => graph.Clean(() => DestroyImmediate(runnerClip));

        protected class PlayableState
        {
            public float time = 0;
            public float transition = 0;
            public float weight = 0;
            public float weightFrom = 0;
            public float weightTo = 0;
            public readonly T clip;
            public readonly Playable playable;
            public PlayableState(Playable playable, T clip, float time = 0, float weight = 0)
            {
                this.playable = playable;
                this.clip = clip;
                this.time = time;
                this.weight = weight;
            }
        }
    }
    public interface IMixRunnerClip
    {
        UnityEngine.Object Target { get; }
        float From { get; }
        float To { get; }
        float Weight { get; }
        float Transition { get; }
        bool Exiting { get; }
        RunnerClip.RunState State { get; }
    }
    public class RunnerClip
    {
        protected readonly RunnerClipData Data;
        protected float Elapsed { get; private set; }

        public RunState State { get; protected set; }

        public RunnerClip(RunnerClipData data)
        {
            Data = data;
            State = RunState.None;
        }
        protected virtual bool Run(RunnerGraph graph)
        {
            if (State != RunState.None) return false;
            if (!Data.Verify())
                Debug.LogError(Time.frameCount + ": invalid of " + Data.GetType());
            Elapsed = 0;
            State = RunState.Running;
            return true;
        }
        protected virtual float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            float subProgress = -1;
            if (State == RunState.None)
                Run(graph);

            if (State == RunState.Running)
            {
                deltaTime *= Data.speed;
                Elapsed += deltaTime;

                subProgress = 1;
                if (Elapsed >= Data.duration)
                {
                    subProgress = (deltaTime + Data.duration - Elapsed) / deltaTime;
                    State = RunState.Done;
                }
            }
            // Debug.Log(Time.frameCount + ": " + this.GetType() + " Running: " + deltaTime + ", " + Elapsed);
            return subProgress;
        }
        public virtual bool DestroyIf(RunState state)
        {
            if (State == state)
            {
                Destroy();
                return true;
            }
            return false;
        }
        public virtual void Destroy()
        {
            State = RunState.Destroyed;
        }

        public enum RunState { None, Running, Done, Destroyed }
        protected static class Scheduler
        {
            public static void Running(RunnerClip clip, RunnerGraph graph, float deltaTime, float progress)
            {
                if (deltaTime >= 0)
                {
                    // Debug.Log(Time.frameCount + ": Scheduler Running: " + progress + ", " + deltaTime);
                    clip.Running(graph, deltaTime, progress);
                }
            }
        }
    }
    private class RunnerClip<T> : RunnerClip where T : RunnerClipData
    {
        protected new readonly T Data;

        public RunnerClip(T data) : base(data) => Data = base.Data as T;
    }

    [Serializable]
    public class GroupClipData : RunnerClipData
    {
        public List<Child> children = new List<Child>();
        public List<UnityEngine.Object> bindings = new List<UnityEngine.Object>();
        public Action onCompleted;

        public void Add(float delay, RunnerClipData data) => children.Add(new Child { delay = delay, data = data });
        public void Bind(UnityEngine.Object binding)
        {
            if (!bindings.Contains(binding))
                bindings.Add(binding);
        }

        public GroupClipData Group(float delay, Action onCompleted = null)
        {
            var data = new GroupClipData
            {
                onCompleted = onCompleted,
            };
            Add(delay, data);
            return data;
        }
        public AnimationClipData Animation(float delay, Animator animator, UnityEngine.AnimationClip animation, float from = 0, float to = float.PositiveInfinity)
        {
            var data = new AnimationClipData
            {
                animator = animator,
                animation = animation,
                from = from,
                to = to,
            };
            Add(delay, data);
            return data;
        }
        public ActiveClipData Active(float delay, UnityEngine.Object target, float duration)
        {
            var data = new ActiveClipData
            {
                target = target,
                duration = duration,
            };
            Add(delay, data);
            return data;
        }
        public AudioClipData Audio(float delay, AudioSource source, UnityEngine.AudioClip clip, float from = 0, float to = float.PositiveInfinity)
        {
            var data = new AudioClipData
            {
                audioSource = source,
                audioClip = clip,
                from = from,
                to = to,
            };
            Add(delay, data);
            return data;
        }
        public MoveClipData Move(float delay, Transform target, Vector3[] path, float duration)
        {
            var data = new MoveClipData
            {
                target = target,
                duration = duration,
            };
            foreach (var position in path)
                data.path.Add(new MoveClipData.Place { position = position });
            Add(delay, data);
            return data;
        }
        public RotateClipData Rotate(float delay, Transform target, Vector3[] path, float duration)
        {
            var data = new RotateClipData
            {
                target = target,
                path = new List<Vector3>(path),
                duration = duration,
            };
            Add(delay, data);
            return data;
        }
        public ScaleClipData Scale(float delay, Transform target, Vector3[] path, float duration)
        {
            var data = new ScaleClipData
            {
                target = target,
                path = new List<Vector3>(path),
                duration = duration,
            };
            Add(delay, data);
            return data;
        }
        public ValueClipData<T> Value<T>(float delay, T from, T to, Action<T> setter, float duration) where T : struct
        {
            var data = new ValueClipData<T>
            {
                from = from,
                to = to,
                setter = setter,
                duration = duration,
            };
            Add(delay, data);
            return data;
        }
        public InvokeClipData Invoke(float delay, Action invoke, float duration = 0)
        {
            var data = new InvokeClipData
            {
                invoke = invoke,
                duration = duration,
            };
            Add(delay, data);
            return data;
        }

        public override bool Verify()
        {
            duration = 0;
            foreach (var child in children)
            {
                child.delay = Mathf.Max(child.delay, 0);
                if (!child.data.Verify())
                    return false;
                duration = Mathf.Max(duration, child.delay + child.data.duration / child.data.speed);
            }
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new GroupClip(this);

        [Serializable]
        public class Child { public float delay; [SerializeReference] public RunnerClipData data; }
    }
    private class GroupClip : RunnerClip<GroupClipData>
    {
        private List<Child> children = new List<Child>();
        private List<UnityEngine.Object> bindings = new List<UnityEngine.Object>();

        public GroupClip(GroupClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res) return false;

            children.Clear();
            foreach (var child in Data.children)
                children.Add(new Child { delay = child.delay, clip = child.data.CreateClip() });
            bindings.Clear();
            bindings.AddRange(Data.bindings);

            return res;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0) return subProgress;

            deltaTime *= Data.speed;
            foreach (var child in children)
            {
                if (child.clip.State == RunState.Done || child.clip.State == RunState.Destroyed)
                    continue;

                var childDeltaTime = deltaTime;
                var childProgress = progress;
                if (child.delay > 0)
                {
                    child.delay -= childDeltaTime;
                    if (child.delay <= 0)
                    {
                        childDeltaTime = -child.delay;
                        childProgress = progress + (1 - progress) * (deltaTime - childDeltaTime) / deltaTime;
                    }
                }
                if (child.delay <= 0)
                    Scheduler.Running(child.clip, graph, childDeltaTime, childProgress);
            }
            if (State == RunState.Done && Data.onCompleted != null)
                graph.Invoke(progress + (1 - progress) * subProgress, Data.onCompleted);

            return subProgress;
        }
        public override bool DestroyIf(RunState state)
        {
            foreach (var child in children)
                child.clip.DestroyIf(state);

            return base.DestroyIf(state);
        }
        public override void Destroy()
        {
            foreach (var child in children)
                child.clip.Destroy();
            children.Clear();
            foreach (var binding in bindings)
                UnityEngine.Object.Destroy(binding);
            bindings.Clear();

            base.Destroy();
        }
        private class Child { public float delay; public RunnerClip clip; }
    }

    [Serializable]
    public class AnimationClipData : RunnerClipData
    {
        public Animator animator;
        public UnityEngine.AnimationClip animation;
        public List<FrameValue> frameSpeeds = new List<FrameValue>();
        public Ending ending = Ending.None;

        [Min(0)]
        public float from = 0;
        [Min(0)]
        public float to = float.PositiveInfinity;
        [Min(0)]
        public float transition = 10;
        [Range(MinWeight, MaxWeight)]
        public float weight = 1;

        public override bool Verify()
        {
            if (!animator || !animation)
                return false;

            var frameCount = animation.length * animation.frameRate;
            from = Mathf.Clamp(from, 0, frameCount);
            to = Mathf.Clamp(to, from, frameCount);
            transition = Mathf.Clamp(transition, 0, to - from);
            weight = weight == 0 ? 1 : Mathf.Clamp(weight, MinWeight, MaxWeight);

            frameSpeeds.RemoveAll(x => x.value == 0 || x.frame < 0 || x.frame >= frameCount || (x.frame == 0 && x.value == 1));
            frameSpeeds.Sort((x, y) => x.frame.CompareTo(y.frame));

            duration = ending == Ending.None ? Len(from, to) / speed : float.PositiveInfinity;

            return base.Verify();
        }
        public override RunnerClip CreateClip() => new AnimationClip(this);
        public float Len(float from, float to)
        {
            var frameCount = animation.length * animation.frameRate;
            from = Mathf.Clamp(from, 0, frameCount);
            to = Mathf.Clamp(to, 0, frameCount);

            float length = 0;
            if (from < to)
            {
                length = frameSpeeds.Count == 0 ? (to - from) / animation.frameRate : 0;
                for (var i = 0; i < frameSpeeds.Count; i++)
                {
                    var frame = frameSpeeds[i].frame;
                    if (frame > from)
                    {
                        var last = i == 0 ? new FrameValue { frame = 0, value = 1 } : frameSpeeds[i - 1];
                        var count = Mathf.Min(frame, to) - Mathf.Max(last.frame, from);
                        length += count / (animation.frameRate * last.value);
                    }
                    if (i == frameSpeeds.Count - 1 && to > frame)
                    {
                        var last = frameSpeeds[i];
                        var count = to - Mathf.Max(last.frame, from);
                        length += count / (animation.frameRate * last.value);
                    }
                    if (frame > to) break;
                }
            }
            return length;
        }
        public float To(float from, float duration)
        {
            var frameCount = animation.length * animation.frameRate;
            from = Mathf.Clamp(from, 0, frameCount);
            duration = Mathf.Max(duration, 0);

            if (duration == 0) return from;
            if (frameSpeeds.Count == 0) return Mathf.Min(duration * animation.frameRate + from, frameCount);

            float to = frameCount;
            float length = 0;
            for (var i = 0; i < frameSpeeds.Count; i++)
            {
                var frame = frameSpeeds[i].frame;
                if (frame > from)
                {
                    var last = i == 0 ? new FrameValue { frame = 0, value = 1 } : frameSpeeds[i - 1];
                    float beg = Mathf.Max(last.frame, from), len = (frame - beg) / (animation.frameRate * last.value);
                    length += len;
                    if (length >= duration)
                    {
                        to = (duration - length + len) / len * (frame - beg) + beg;
                        break;
                    }
                }
                if (i == frameSpeeds.Count - 1 && length < duration)
                {
                    float beg = Mathf.Max(from, frame), len = (frameCount - frame) / (animation.frameRate * frameSpeeds[i].value);
                    length += len;
                    if (length >= duration)
                    {
                        to = (duration - length + len) / len * (frameCount - frame) + beg;
                        break;
                    }
                }
            }
            return to;
        }

        [Serializable]
        public enum Ending { None, Loop, Keep }
        [Serializable]
        public class FrameValue { public float frame; public float value; }
    }
    private class AnimationClip : RunnerClip<AnimationClipData>, IMixRunnerClip
    {
        public UnityEngine.Object Target => Data.animator;
        public float From => Data.from;
        public float To => Data.to;
        public float Weight => Data.weight;
        public float Transition => Data.transition;
        public bool Exiting { get; private set; }

        private AnimationPlayable playable;
        private float cycleDuration;

        public AnimationClip(AnimationClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res || !Data.animator) return res;

            cycleDuration = Data.Len(Data.from, Data.to) / Data.speed;
            Exiting = false;
            playable = AnimationPlayable.Create(graph, this);
            return res;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var lastState = State;
            var lastExiting = Exiting;
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0 || playable == null || !playable.IsValid()) return subProgress;

            var frame = Data.To(Data.from, Data.ending == AnimationClipData.Ending.Loop ? Elapsed % cycleDuration : Elapsed);
            frame = Mathf.Min(frame, Data.to);
            Exiting = Data.ending == AnimationClipData.Ending.None && frame + Data.transition > Data.to;
            playable.SetTime(this, frame, State != lastState, Exiting != lastExiting);
            // Debug.Log(Time.frameCount + ": SetTime: " + to + "," + time);
            return subProgress;
        }
        public override void Destroy()
        {
            if (playable != null)
            {
                playable.Destroy(this);
                playable = null;
            }
            base.Destroy();
        }

        private class AnimationPlayable : RunnerPlayable<AnimationClip>
        {
            public override bool ContainsState(string state, params string[] states)
            {
                var names = new List<string>(states);
                names.Add(state);

                var res = false;
                foreach (var name in names)
                {
                    foreach (var st in this.states)
                    {
                        res = st.clip.Data.animation.name == name;
                        if (!res) return false;
                    }
                }
                return res;
            }
            public override bool ContainsState(UnityEngine.Object state, params UnityEngine.Object[] states)
            {
                var objs = new List<UnityEngine.Object>(states);
                objs.Add(state);

                var res = false;
                foreach (var obj in objs)
                {
                    if (!obj) continue;
                    foreach (var st in this.states)
                    {
                        res = st.clip.Data.animation == obj;
                        if (!res) return false;
                    }
                }
                return res;
            }
            protected override void SetTime(PlayableState state, float time) => state.playable.SetTime(time / state.clip.Data.animation.frameRate);
            public static AnimationPlayable Create(RunnerGraph graph, AnimationClip runnerClip)
            {
                var playable = RunnerGraph.RunnerPlayable.Create<AnimationPlayable>(graph, runnerClip.Target, playable =>
                {
                    playable.output = AnimationPlayableOutput.Create(graph.playableGraph, runnerClip.Data.animator.name, runnerClip.Data.animator);
                    playable.mixer = AnimationMixerPlayable.Create(graph.playableGraph);
                    playable.output.SetSourcePlayable(playable.mixer);
                });
                playable.CreateState(AnimationClipPlayable.Create(graph.playableGraph, runnerClip.Data.animation), runnerClip, runnerClip.Data.from);
                return playable;
            }
        }
    }

    [Serializable]
    public class ActiveClipData : RunnerClipData
    {
        public UnityEngine.Object target;
        public Action<bool> activator { get; private set; }

        public override bool Verify()
        {
            if (!target) return false;

            if (target is GameObject || target is Transform)
            {
                var gameObject = target is GameObject ? (GameObject)target : ((Transform)target).gameObject;
                activator = enabled => gameObject.SetActive(enabled);
            }
            else
            {
                if (target is Behaviour behaviour)
                    activator = enabled => behaviour.enabled = enabled;
                else if (target is Renderer renderer)
                    activator = enabled => renderer.enabled = enabled;
                else if (target is Collider collider)
                    activator = enabled => collider.enabled = enabled;
                else
                {
                    var propertyInfo = target.GetType().GetProperty("enabled");
                    if (propertyInfo != null && propertyInfo.CanWrite)
                        activator = enabled => propertyInfo.SetValue(target, enabled);
                }
            }
            activator?.Invoke(false);
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new ActiveClip(this);
    }
    private class ActiveClip : RunnerClip<ActiveClipData>
    {
        private bool active = false;
        private ParticleSystemPlayable particleSystemPlayable;

        public ActiveClip(ActiveClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res || !Data.target) return res;

            if (Data.target is GameObject || Data.target is Transform)
            {
                var gameObject = Data.target is GameObject ? (GameObject)Data.target : ((Transform)Data.target).gameObject;
                particleSystemPlayable = new ParticleSystemPlayable(gameObject);
                if (particleSystemPlayable.ParticleSystemCount == 0)
                    particleSystemPlayable = null;
                particleSystemPlayable?.Stop();
            }
            Data.activator?.Invoke(active);
            return res;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0 || !Data.target) return subProgress;

            if (active != (State == RunState.Running))
            {
                active = !active;
                Data.activator?.Invoke(active);
                particleSystemPlayable?.Stop();
            }
            particleSystemPlayable?.SetTime(Elapsed);
            return subProgress;
        }

        private class ParticleSystemPlayable
        {
            public int ParticleSystemCount => states.Length;
            private GameObject gameObject;
            private ParticleSystemState[] states;
            private float lastTime = float.MinValue;
            public ParticleSystemPlayable(GameObject gameObject)
            {
                this.gameObject = gameObject;
                var particleSystems = gameObject.GetComponentsInChildren<ParticleSystem>();
                states = new ParticleSystemState[particleSystems.Length];
                for (var index = 0; index < states.Length; index++)
                {
                    var state = states[index] = new ParticleSystemState();
                    state.particleSystem = particleSystems[index];
                }
            }
            public void Stop()
            {
                foreach (var state in states)
                {
                    Stop(state.particleSystem);
                    state.lastParticleTime = float.MinValue;
                }
            }

            private static void Stop(ParticleSystem particleSystem)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                for (int i = 0; i < particleSystem.subEmitters.subEmittersCount; i++)
                    Stop(particleSystem.subEmitters.GetSubEmitterSystem(i));
            }
            public void SetTime(float time)
            {
                foreach (var state in states)
                {
                    bool dirty = false, restart = true;
                    float simulateTime = time;
                    // if particle system time has changed externally, a re-sync is needed
                    if (lastTime > time || !Mathf.Approximately(state.particleSystem.time, state.lastParticleTime))
                    {
                        dirty = true;
                        restart = true;
                        simulateTime = time;
                    }
                    else if (lastTime < time)
                    {
                        dirty = true;
                        restart = false;
                        simulateTime = time - lastTime;
                    }
                    if (dirty)
                    {
                        if (restart)
                            state.particleSystem.Simulate(0, false, true, false);

                        // simulating by too large a time-step causes sub-emitters not to work, and loops not to simulate correctly
                        float maxTime = Time.maximumDeltaTime;
                        while (simulateTime > maxTime)
                        {
                            state.particleSystem.Simulate(maxTime, false, false, false);
                            simulateTime -= maxTime;
                        }
                        if (simulateTime > 0)
                            state.particleSystem.Simulate(simulateTime, false, false, false);
                    }
                    state.lastParticleTime = state.particleSystem.time;
                }
                lastTime = time;
            }

            private class ParticleSystemState
            {
                public ParticleSystem particleSystem;
                public float lastParticleTime;
            }
        }
    }

    [Serializable]
    public class AudioClipData : RunnerClipData
    {
        public AudioSource audioSource;
        public UnityEngine.AudioClip audioClip;
        public bool loop;

        [Min(0)]
        public float from = 0;
        [Min(0)]
        public float to = float.PositiveInfinity;
        [Min(0)]
        public float transition = 0.1f;
        [Range(MinWeight, MaxWeight)]
        public float weight = 1;

        public override bool Verify()
        {
            if (!audioSource || !audioClip) return false;
            from = Mathf.Max(from, 0);
            to = Mathf.Min(to, audioClip.samples / audioClip.frequency);
            weight = weight == 0 ? 1 : Mathf.Clamp(weight, MinWeight, MaxWeight);

            duration = loop ? float.PositiveInfinity : (to - from);
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new AudioClip(this);
    }
    private class AudioClip : RunnerClip<AudioClipData>, IMixRunnerClip
    {
        public UnityEngine.Object Target => Data.audioSource;
        public float From => Data.from;
        public float To => Data.to;
        public float Weight => Data.weight;
        public float Transition => Data.transition;
        public bool Exiting { get; private set; }

        private AudioPlayable playable;

        public AudioClip(AudioClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (res)
            {
                playable = AudioPlayable.Create(graph, this);
            }
            return res;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var lastState = State;
            var lastExiting = Exiting;
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0 || playable == null || !playable.IsValid()) return subProgress;

            Exiting = !Data.loop && Elapsed + Data.transition > Data.to;
            var time = Mathf.Min(Data.from + (Data.loop ? Elapsed % (Data.to - Data.from) : Elapsed), Data.to);
            playable.SetTime(this, time, State != lastState, Exiting != lastExiting);
            return subProgress;
        }
        public override void Destroy()
        {
            if (playable != null)
            {
                playable.Destroy(this);
                playable = null;
            }
            base.Destroy();
        }
        private class AudioPlayable : RunnerPlayable<AudioClip>
        {
            public override bool ContainsState(string state, params string[] states)
            {
                var names = new List<string>(states);
                names.Add(state);

                var res = false;
                foreach (var name in names)
                {
                    foreach (var st in this.states)
                    {
                        res = st.clip.Data.audioClip.name == name;
                        if (!res) return false;
                    }
                }
                return res;
            }
            public override bool ContainsState(UnityEngine.Object state, params UnityEngine.Object[] states)
            {
                var objs = new List<UnityEngine.Object>(states);
                objs.Add(state);

                var res = false;
                foreach (var obj in objs)
                {
                    if (!obj) continue;
                    foreach (var st in this.states)
                    {
                        res = st.clip.Data.audioClip == obj;
                        if (!res) return false;
                    }
                }
                return res;
            }
            public static AudioPlayable Create(RunnerGraph graph, AudioClip runnerClip)
            {
                var playable = RunnerGraph.RunnerPlayable.Create<AudioPlayable>(graph, runnerClip.Target, playable =>
                {
                    playable.output = AudioPlayableOutput.Create(graph.playableGraph, runnerClip.Data.audioSource.name, runnerClip.Data.audioSource);
                    playable.mixer = AudioMixerPlayable.Create(graph.playableGraph);
                    playable.output.SetSourcePlayable(playable.mixer);
                });
                playable.CreateState(AudioClipPlayable.Create(graph.playableGraph, runnerClip.Data.audioClip, runnerClip.Data.loop), runnerClip, runnerClip.Data.from);
                return playable;
            }
        }
    }
    private class Navigation
    {
        private Vector3[] path;
        private bool isCurve;
        private float length = 0;

        private float[] slices;

        public Navigation(List<Vector3> locations, bool isCurve = false)
        {
            var unepeatedLocations = new List<Vector3>();
            foreach (var position in locations)
            {
                if (unepeatedLocations.Count == 0 || position != unepeatedLocations[unepeatedLocations.Count - 1])
                    unepeatedLocations.Add(position);
            }
            path = unepeatedLocations.ToArray();
            this.isCurve = isCurve;
            length = 0;

            if (path.Length > 1)
            {
                if (isCurve)
                {
                    var last = path[0];
                    for (float d = 0.001f, f = 0; f <= 1 + 2 * d; f += d)
                    {
                        var curr = Lerp(f);
                        length += Vector3.Distance(last, curr);
                        last = curr;
                    }
                }
                else
                {
                    slices = new float[path.Length - 1];
                    for (var i = 0; i < slices.Length; i++)
                    {
                        var slice = Vector3.Distance(path[i], path[i + 1]);
                        slices[i] = slice;
                        length += slice;
                    }
                }
            }
        }
        public Vector3 Lerp(float t)
        {
            if (path.Length == 0) return Vector3.zero;
            if (path.Length == 1) return path[0];

            if (isCurve)
            {
                if (t < 0)
                    return Vector3.LerpUnclamped(path[0], path[1], t);
                else if (t > 1)
                    return Vector3.LerpUnclamped(path[path.Length - 2], path[path.Length - 1], t);

                int numSections = path.Length - 1;
                int currPt = Mathf.Min((int)Mathf.Floor(t * numSections), numSections - 1);
                float u = t * numSections - currPt;

                Vector3 a = currPt == 0 ? path[0] : path[currPt - 1];
                Vector3 b = path[currPt];
                Vector3 c = path[currPt + 1];
                Vector3 d = currPt + 2 > path.Length - 1 ? path[path.Length - 1] : path[currPt + 2];

                return .5f * (
                    (-a + 3 * b - 3 * c + d) * (u * u * u)
                    + (2 * a - 5 * b + 4 * c - d) * (u * u)
                    + (-a + c) * u
                    + 2 * b
                );
            }
            else
            {
                var curr = length * t;
                if (t <= 0)
                    return Vector3.LerpUnclamped(path[0], path[1], curr / slices[0]);
                else if (t >= 1)
                    return Vector3.LerpUnclamped(path[path.Length - 2], path[path.Length - 1], 1 + (curr - length) / slices[slices.Length - 1]);

                var pass = 0f;
                for (var i = 0; i < slices.Length; i++)
                {
                    var slice = slices[i];
                    if (curr >= pass && curr <= pass + slice)
                        return Vector3.Lerp(path[i], path[i + 1], (curr - pass) / slice);
                    pass += slice;
                }
                Debug.LogError(Time.frameCount + ": Navigation.Lerp error " + t);
                return path[path.Length - 1];
            }
        }
#if UNITY_EDITOR
        public void Trace(Transform parent)
        {
            if (isCurve)
            {
                var last = path[0];
                if (parent) last = parent.TransformPoint(last);
                for (float d = 0.001f, f = 0; f <= 1 + 2 * d; f += d)
                {
                    var curr = Lerp(f);
                    if (parent) curr = parent.TransformPoint(curr);
                    Debug.DrawLine(last, curr, Color.blue, 1);
                    last = curr;
                }
            }
            else
            {
                var last = path[0];
                if (parent) last = parent.TransformPoint(last);
                for (var i = 1; i < path.Length; i++)
                {
                    var curr = path[i];
                    if (parent) curr = parent.TransformPoint(curr);
                    Debug.DrawLine(last, curr, Color.blue, 1);
                    last = curr;
                }
            }
        }
#endif
    }

    [Serializable]
    public class MoveClipData : RunnerClipData
    {
        public Transform target;
        public List<Place> path = new List<Place>();
        public EaseFunction ease;
        public bool isLocal;
        public bool isCurve;

        public override bool Verify()
        {
            duration = duration < 0 ? 0 : duration;
            for (var i = 1; i < path.Count; i++)
            {
                if (path[i].position == path[i - 1].position)
                    path.RemoveAt(i--);
            }

            if (!target || path.Count == 0 || path.Exists(p => (p.lookAtType == Place.LookAtType.Transform && !p.lookAtTransform)))
                return false;
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new MoveClip(this);

        [Serializable]
        public class Place
        {
            public enum LookAtType { None, Path, Transform, Position }

            public Vector3 position;
            public LookAtType lookAtType = LookAtType.None;
            public Vector3 lookAtPosition;
            public Transform lookAtTransform;
            public Vector3 lookAtUp = Vector3.up;
        }
    }
    private class MoveClip : RunnerClip<MoveClipData>
    {
        private Navigation navigation;
        private List<MoveClipData.Place> path;

        public MoveClip(MoveClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res || !Data.target) return res;

            path = new List<MoveClipData.Place>(Data.path);
            path.Insert(0, new MoveClipData.Place { position = Data.isLocal ? Data.target.localPosition : Data.target.position });

            var navigationPath = new List<Vector3>();
            foreach (var place in path) navigationPath.Add(place.position);
            navigation = new Navigation(navigationPath, Data.isCurve);
            return true;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0 || !Data.target) return subProgress;

            var t = Mathf.Clamp01(Elapsed / Data.duration);
            var ease = Data.ease ?? Ease.Linear;
            var et = ease(t);
            if (Data.isLocal) Data.target.localPosition = navigation.Lerp(et);
            else Data.target.position = navigation.Lerp(et);
            // Debug.Log(Time.frameCount + ": position: " + Data.target.position + ", localPosition: " + Data.target.localPosition + ", time: " + et);

            var place = path[(int)((path.Count - 1) * et)];
            if (place.lookAtType != MoveClipData.Place.LookAtType.None && (place.lookAtType != MoveClipData.Place.LookAtType.Transform || place.lookAtTransform))
            {
                var forward = Vector3.zero;
                if (place.lookAtType == MoveClipData.Place.LookAtType.Transform)
                    forward = place.lookAtTransform.position - Data.target.position;
                else if (place.lookAtType == MoveClipData.Place.LookAtType.Position)
                    forward = place.lookAtPosition - (Data.isLocal ? Data.target.localPosition : Data.target.position);
                else
                    forward = t > 0.999f ? navigation.Lerp(ease(1)) - navigation.Lerp(ease(0.999f)) : navigation.Lerp(ease(t + 0.001f)) - navigation.Lerp(ease(t));

                Data.target.rotation = Quaternion.LookRotation(forward, place.lookAtUp);
            }

#if UNITY_EDITOR
            navigation.Trace(Data.target.parent);
#endif
            return subProgress;
        }
    }

    [Serializable]
    public class RotateClipData : RunnerClipData
    {
        public Transform target;
        public List<Vector3> path = new List<Vector3>();
        public EaseFunction ease;
        public bool isLocal;
        public bool isCurve;

        public override bool Verify()
        {
            duration = duration < 0 ? 0 : duration;
            for (var i = 1; i < path.Count; i++)
            {
                if (path[i] == path[i - 1])
                    path.RemoveAt(i--);
            }

            if (!target || path.Count == 0)
                return false;
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new RotateClip(this);
    }
    private class RotateClip : RunnerClip<RotateClipData>
    {
        private Navigation navigation;

        public RotateClip(RotateClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res || !Data.target) return res;

            var path = new List<Vector3>(Data.path);
            path.Insert(0, Data.isLocal ? Data.target.localEulerAngles : Data.target.eulerAngles);
            for (var index = 0; index < path.Count; index++)
            {
                var curr = path[index];
                curr.x %= 360;
                curr.x = curr.x > 180 ? curr.x - 360 : (curr.x < -180 ? curr.x + 360 : curr.x);
                curr.y %= 360;
                curr.y = curr.y > 180 ? curr.y - 360 : (curr.y < -180 ? curr.y + 360 : curr.y);
                curr.z %= 360;
                curr.z = curr.z > 180 ? curr.z - 360 : (curr.z < -180 ? curr.z + 360 : curr.z);
                path[index] = curr;
            }
            navigation = new Navigation(path, Data.isCurve);
            return true;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0 || !Data.target) return subProgress;

            var t = Mathf.Clamp01(Elapsed / Data.duration);
            var et = Data.ease != null ? Data.ease(t) : t;
            if (Data.isLocal)
                Data.target.localEulerAngles = navigation.Lerp(et);
            else
                Data.target.eulerAngles = navigation.Lerp(et);
            return subProgress;
        }
    }

    [Serializable]
    public class ScaleClipData : RunnerClipData
    {
        public Transform target;
        public List<Vector3> path = new List<Vector3>();
        public EaseFunction ease;
        public bool isCurve;

        public override bool Verify()
        {
            duration = duration < 0 ? 0 : duration;
            for (var i = 1; i < path.Count; i++)
            {
                if (path[i] == path[i - 1])
                    path.RemoveAt(i--);
            }

            if (!target || path.Count == 0)
                return false;
            return base.Verify();
        }

        public override RunnerClip CreateClip() => new ScaleClip(this);
    }
    private class ScaleClip : RunnerClip<ScaleClipData>
    {
        private Navigation navigation;
        public ScaleClip(ScaleClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res || !Data.target) return res;

            var path = new List<Vector3>(Data.path);
            path.Insert(0, Data.target.localScale);
            navigation = new Navigation(path, Data.isCurve);
            return true;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0 || !Data.target) return subProgress;

            var t = Mathf.Clamp01(Elapsed / Data.duration);
            var et = Data.ease != null ? Data.ease(t) : t;
            Data.target.localScale = navigation.Lerp(et);
            return subProgress;
        }
    }

    [Serializable]
    public class ValueClipData<T> : RunnerClipData where T : struct
    {
        public T from, to;
        public EaseFunction ease;
        public Action<T> setter;

        public override bool Verify()
        {
            Debug.Assert(Lerp != null, "??????????????????:" + typeof(T).Name);
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new ValueClip<T>(this);

        public static readonly Func<T, T, float, T> Lerp = Lerpper.Lerp<T>();
        private static class Lerpper
        {
            private static class Impl<U> where U : struct { public static Func<U, U, float, U> lerp; }
            static Lerpper()
            {
                Impl<float>.lerp = Mathf.Lerp;
                Impl<Vector2>.lerp = Vector2.Lerp;
                Impl<Vector3>.lerp = Vector3.Lerp;
                Impl<Vector4>.lerp = Vector4.Lerp;
                Impl<Color>.lerp = Color.Lerp;
                Impl<Color32>.lerp = Color32.Lerp;
                Impl<Quaternion>.lerp = Quaternion.Lerp;
            }
            public static Func<U, U, float, U> Lerp<U>() where U : struct => Impl<U>.lerp;
        }
    }
    private class ValueClip<T> : RunnerClip<ValueClipData<T>> where T : struct
    {
        public ValueClip(ValueClipData<T> data) : base(data) { }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0) return subProgress;

            var t = Mathf.Clamp01(Elapsed / Data.duration);
            var et = Data.ease != null ? Data.ease(t) : t;

            var value = ValueClipData<T>.Lerp(Data.from, Data.to, et);
            graph.Invoke(progress, () => Data.setter(value));
            return subProgress;
        }
    }

    [Serializable]
    public class InvokeClipData : RunnerClipData
    {
        public Action invoke;

        public override bool Verify()
        {
            speed = 1;
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new InvokeClip(this);
    }
    private class InvokeClip : RunnerClip<InvokeClipData>
    {
        public InvokeClip(InvokeClipData data) : base(data) { }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress)
        {
            var subProgress = base.Running(graph, deltaTime, progress);
            if (subProgress < 0) return subProgress;

            graph.Invoke(progress, Data.invoke);
            return subProgress;
        }
    }
}
