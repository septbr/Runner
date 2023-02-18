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
    private void Update() => director.Evaluate(Time.deltaTime * Speed);
    private void OnDestroy() => director.Destroy();

    public RunnerClip Run(RunnerClipData data, float delay = 0) => director.Run(data, delay);

    public class RunnerGraph
    {
        public readonly PlayableGraph playableGraph;
        private RunnerGraph(string name) => playableGraph = PlayableGraph.Create(name);
        public static RunnerGraph Create(string name) => new RunnerGraph(name);
    }

    private class RunnerDirector : PlayableBehaviour
    {
        private RunnerGraph graph;
        private List<Node> nodes = new List<Node>();
        private FrameInvoker invoker = new FrameInvoker();

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

        public RunnerClip Run(RunnerClipData data, float delay = 0)
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

        public void Evaluate(float time) => graph.playableGraph.Evaluate(time);
        public void Destroy()
        {
            foreach (var node in nodes)
                node.clip.Destroy();
            nodes.Clear();
            invoker.Clear();
            graph.playableGraph.Destroy();
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
                    FrameScheduler.Running(node.clip, graph, deltaTime, progress, invoker);
            }
        }
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            invoker.InvokeImmediate();
            for (var index = 0; index < nodes.Count; index++)
            {
                var clip = nodes[index].clip;
                if (clip.DestroyIf(RunnerClip.RunState.Done))
                    nodes.RemoveAt(index--);
            }
        }

        private class Node { public float delay; public RunnerClip clip; }
        private class FrameInvoker : IInvoker
        {
            private List<Tuple<float, Action>> invokers = new List<Tuple<float, Action>>();
            public void Invoke(float time, Action invoke)
            {
                var pos = invokers.Count;
                for (var index = 0; index < invokers.Count; index++)
                {
                    if (time < invokers[index].Item1)
                    {
                        pos = index;
                        break;
                    }
                }
                invokers.Insert(pos, new Tuple<float, Action>(time, invoke));
            }
            public void Clear() => invokers.Clear();
            public void InvokeImmediate()
            {
                foreach (var invoker in invokers)
                    invoker.Item2();
                invokers.Clear();
            }
        }
        private class FrameScheduler : RunnerClip
        {
            private FrameScheduler(RunnerClipData data) : base(data) => throw new NotImplementedException();
            public static void Running(RunnerClip clip, RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
                => Scheduler.Running(clip, graph, deltaTime, progress, invoker);
        }
    }
    public interface IInvoker
    {
        void Invoke(float time, Action handle);
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
        protected virtual float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
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
            public static void Running(RunnerClip clip, RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
            {
                if (deltaTime >= 0)
                {
                    // Debug.Log(Time.frameCount + ": Scheduler Running: " + progress + ", " + deltaTime);
                    clip.Running(graph, deltaTime, progress, invoker);
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

        public void Add(RunnerClipData data, float delay = 0) => children.Add(new Child { delay = delay, data = data });

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

        public class Child { public float delay; public RunnerClipData data; }
    }
    private class GroupClip : RunnerClip<GroupClipData>
    {
        private List<Child> children = new List<Child>();

        public GroupClip(GroupClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res) return false;

            children.Clear();
            foreach (var child in Data.children)
                children.Add(new Child { delay = child.delay, clip = child.data.CreateClip() });
            return res;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

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
                    Scheduler.Running(child.clip, graph, childDeltaTime, childProgress, invoker);
            }

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
        public float transition = 5;
        [Range(-1000, 1000)]
        public float weight = 1;

        public override bool Verify()
        {
            if (!animator || !animation)
                return false;

            var frameCount = animation.length * animation.frameRate;
            from = Mathf.Clamp(from, 0, frameCount);
            to = Mathf.Clamp(to, from, frameCount);
            transition = Mathf.Clamp(transition, 0, to - from);
            weight = weight == 0 ? 1 : weight;

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
    private class AnimationClip : RunnerClip<AnimationClipData>
    {
        private Playable playable;
        private float cycleDuration;

        private bool exiting;

        public AnimationClip(AnimationClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (!res) return false;

            cycleDuration = Data.Len(Data.from, Data.to) / Data.speed;
            exiting = false;
            playable = Playable.Create(graph, this);
            return res;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var lastState = State;
            var lastExiting = exiting;
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

            var frame = Data.To(Data.from, Data.ending == AnimationClipData.Ending.Loop ? Elapsed % cycleDuration : Elapsed);
            exiting = Data.ending == AnimationClipData.Ending.None && frame + Data.transition > Data.to;
            playable.SetFrame(this, frame, State != lastState, exiting != lastExiting);
            // Debug.Log(Time.frameCount + ": SetTime: " + to + "," + time);
            return subProgress;
        }
        public override void Destroy()
        {
            playable.Destroy(this);
            base.Destroy();
        }

        private class Playable
        {
            private static List<Playable> playables = new List<Playable>();

            private RunnerGraph graph;
            private AnimationPlayableOutput output;
            private AnimationMixerPlayable mixer;
            private List<PlayableState> states = new List<PlayableState>();

            private Playable() { }
            public static Playable Create(RunnerGraph graph, AnimationClip runnerClip)
            {
                Playable playable = null;
                foreach (var p in playables)
                {
                    if (p.graph == graph && p.output.GetTarget() == runnerClip.Data.animator)
                    {
                        playable = p;
                        break;
                    }
                }
                if (playable == null)
                {
                    playable = new Playable();
                    playable.graph = graph;
                    playable.output = AnimationPlayableOutput.Create(graph.playableGraph, runnerClip.Data.animator.name, runnerClip.Data.animator);
                    playable.mixer = AnimationMixerPlayable.Create(graph.playableGraph);
                    playable.output.SetSourcePlayable(playable.mixer);
                    playables.Add(playable);
                }
                var state = new PlayableState();
                state.frame = runnerClip.Data.from;
                state.weight = 0;
                state.clip = runnerClip;
                state.playable = AnimationClipPlayable.Create(graph.playableGraph, runnerClip.Data.animation);
                state.playable.SetTime(state.frame / runnerClip.Data.animation.frameRate);

                playable.states.Add(state);
                playable.mixer.AddInput(state.playable, 0, state.weight);

                return playable;
            }

            public void SetFrame(AnimationClip runnerClip, float frame, bool playableStateChanged, bool exitingChanged)
            {
                foreach (var state in states)
                {
                    if (state.clip == runnerClip)
                    {
                        var lastFrame = state.frame;
                        state.frame = frame;
                        state.playable.SetTime(frame / runnerClip.Data.animation.frameRate);

                        var desiredWeightDirty = playableStateChanged || exitingChanged;
                        if (desiredWeightDirty)
                            UpdateWeightTo();

                        if (desiredWeightDirty || state.transition != -1)
                        {
                            var deltaFrame = frame - lastFrame;
                            if (deltaFrame < 0) deltaFrame = frame + runnerClip.Data.to - lastFrame;
                            UpdateWeight(deltaFrame);
                        }
                        return;
                    }
                }
            }
            private void UpdateWeightTo()
            {
                const float LeftLimitWeight = -1000, RightLimitWeight = 1000;
                bool hasRightLimit = false, hasRightCount = false, hasUpLeftLimit = false;
                for (var index = 0; index < states.Count; index++)
                {
                    var state = states[index];
                    var weightTo = state.clip.exiting || state.clip.State != RunState.Running ? 0 : state.clip.Data.weight;
                    if (weightTo != 0)
                    {
                        if (weightTo >= RightLimitWeight)
                        {
                            hasRightLimit = true;
                            break;
                        }
                        if (weightTo > 0)
                            hasRightCount = true;
                        else if (weightTo > LeftLimitWeight)
                            hasUpLeftLimit = true;
                    }
                }
                for (var index = 0; index < states.Count; index++)
                {
                    var state = states[index];
                    var weightTo = state.clip.exiting || state.clip.State != RunState.Running ? 0 : state.clip.Data.weight;
                    if (weightTo != 0)
                    {
                        if (hasRightLimit)
                            weightTo = weightTo >= RightLimitWeight ? 1 : 0;
                        else if (hasRightCount)
                            weightTo = weightTo > 0 ? weightTo : 0;
                        else if (hasUpLeftLimit)
                            weightTo = weightTo <= LeftLimitWeight ? 0 : weightTo + RightLimitWeight;
                        else
                            weightTo = 1;
                    }
                    state.weightTo = weightTo;
                }
            }
            /// <summary>
            /// 节点预设值为0, 节点权重也为0, 且节点预设值越高, 节点权重越高
            /// 当有节点预设值>0时, <0的预设值节点权重都为0;
            /// 当有节点预设值≥1000时, 其他<1000节点权重都为0;
            /// 当有节点预设值≤-1000时, 节点权重为0;
            /// 每个节点权重以渐变形式达到预设值平衡值, 平衡值和恒等于1。
            /// 节点权重渐变时长固定1s, 当期望权重发生变化时, 重置渐变
            /// </summary>
            private void UpdateWeight(float deltaFrame)
            {
                float weightTotal = 0;
                for (var index = 0; index < states.Count; index++)
                {
                    var state = states[index];
                    var weight = state.weightTo;
                    if (state.clip.Data.transition > 0 && state.weight != state.weightTo && state.transition == -1)
                    {
                        state.transition = 0;
                        state.weightFrom = state.weight;
                    }
                    if (state.transition != -1)
                    {
                        state.transition += deltaFrame;
                        weight = Mathf.Lerp(state.weightFrom, state.weightTo, state.transition / state.clip.Data.transition);
                        if (state.transition >= state.clip.Data.transition) state.transition = -1;
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
            public void Destroy(AnimationClip runnerClip)
            {
                for (var index = 0; index < states.Count; index++)
                {
                    var state = states[index];
                    if (state.clip == runnerClip)
                    {
                        states.RemoveAt(index);
                        state.playable.Destroy();

                        if (states.Count == 0)
                        {
                            mixer.Destroy();
                            graph.playableGraph.DestroyOutput(output);
                            playables.Remove(this);
                            return;
                        }

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

            private class PlayableState
            {
                public float frame;
                public float transition;
                public float weight;
                public float weightFrom;
                public float weightTo;
                public AnimationClip clip;
                public AnimationClipPlayable playable;
            }
        }
    }

    [Serializable]
    public class ActiveClipData : RunnerClipData
    {
        public UnityEngine.Object target;
        public override bool Verify()
        {
            if (!target) return false;
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new ActiveClip(this);
    }
    private class ActiveClip : RunnerClip<ActiveClipData>
    {
        private bool active = false;
        private ParticleSystemPlayable particleSystemPlayable;

        private Action<bool> _activator;
        public ActiveClip(ActiveClipData data) : base(data)
        {
            if (Data.target is GameObject || Data.target is Transform)
            {
                var gameObject = Data.target is GameObject ? (GameObject)Data.target : ((Transform)Data.target).gameObject;
                particleSystemPlayable = new ParticleSystemPlayable(gameObject);
                if (particleSystemPlayable.ParticleSystemCount == 0)
                    particleSystemPlayable = null;

                _activator = enabled => gameObject.SetActive(enabled);
            }
            else
            {
                if (Data.target is Behaviour behaviour)
                    _activator = enabled => behaviour.enabled = enabled;
                else if (Data.target is Renderer renderer)
                    _activator = enabled => renderer.enabled = enabled;
                else if (Data.target is Collider collider)
                    _activator = enabled => collider.enabled = enabled;
                else
                {
                    var propertyInfo = Data.target.GetType().GetProperty("enabled");
                    if (propertyInfo != null && propertyInfo.CanWrite)
                        _activator = enabled => propertyInfo.SetValue(Data.target, enabled);
                }
            }
            _activator?.Invoke(active);
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

            if (active != (State == RunState.Running))
            {
                active = !active;
                _activator?.Invoke(active);
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
                if (!gameObject.activeInHierarchy) return;
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
    public class SoundClipData : RunnerClipData
    {
        public AudioSource audioSource;
        public AudioClip audioClip;
        public bool loop;
        public override bool Verify()
        {
            if (!audioSource || !audioClip) return false;
            duration = loop ? float.PositiveInfinity : audioClip.samples / audioClip.frequency;
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new SoundClip(this);
    }
    private class SoundClip : RunnerClip<SoundClipData>
    {
        private RunnerGraph graph;
        private AudioPlayableOutput output;
        private AudioClipPlayable playable;
        private float cycleDuration;

        public SoundClip(SoundClipData data) : base(data) { }
        protected override bool Run(RunnerGraph graph)
        {
            var res = base.Run(graph);
            if (res)
            {
                cycleDuration = Data.audioClip.samples / Data.audioClip.frequency;
                this.graph = graph;
                output = AudioPlayableOutput.Create(graph.playableGraph, Data.audioSource.name, Data.audioSource);
                playable = AudioClipPlayable.Create(graph.playableGraph, Data.audioClip, Data.loop);
                output.SetSourcePlayable(playable);
            }
            return res;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

            playable.SetTime(Data.loop ? Elapsed % cycleDuration : Elapsed);
            return subProgress;
        }
        public override void Destroy()
        {
            playable.Destroy();
            graph.playableGraph.DestroyOutput(output);
            base.Destroy();
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
            Debug.Assert(locations.Count > 1, Time.frameCount + ": Expecting at least 2 points");

            path = locations.ToArray();
            this.isCurve = isCurve;
            length = 0;

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
        public Vector3 Lerp(float t)
        {
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
            if (!res) return false;

            path = new List<MoveClipData.Place>(Data.path);
            if ((Data.isLocal && path[0].position != Data.target.localPosition) || (!Data.isLocal && path[0].position != Data.target.position))
                path.Insert(0, new MoveClipData.Place { position = Data.isLocal ? Data.target.localPosition : Data.target.position });

            var navigationPath = new List<Vector3>();
            foreach (var place in path) navigationPath.Add(place.position);
            navigation = new Navigation(navigationPath, Data.isCurve);
            return true;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

            var t = Mathf.Clamp01(Elapsed / Data.duration);
            var ease = Data.ease ?? Ease.Linear;
            var et = ease(t);
            if (Data.isLocal) Data.target.localPosition = navigation.Lerp(et);
            else Data.target.position = navigation.Lerp(et);
            // Debug.Log(Time.frameCount + ": position: " + Data.target.position + ", localPosition: " + Data.target.localPosition + ", time: " + et);

            var place = path[(int)((path.Count - 1) * et)];
            if (place.lookAtType != MoveClipData.Place.LookAtType.None)
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
            if (!res) return false;

            var path = new List<Vector3>(Data.path);
            if ((Data.isLocal && path[0] != Data.target.localEulerAngles) || (!Data.isLocal && path[0] != Data.target.eulerAngles))
                path.Insert(0, Data.isLocal ? Data.target.localEulerAngles : Data.target.eulerAngles);

            navigation = new Navigation(path, Data.isCurve);
            return true;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

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
            if (!res) return false;

            var path = new List<Vector3>(Data.path);
            if (path[0] != Data.target.localScale)
                path.Insert(0, Data.target.localScale);
            navigation = new Navigation(path, Data.isCurve);
            return true;
        }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

            var t = Mathf.Clamp01(Elapsed / Data.duration);
            var et = Data.ease != null ? Data.ease(t) : t;
            Data.target.localScale = navigation.Lerp(et);
            return subProgress;
        }
    }

    [Serializable]
    public class ValueClipData<T> : RunnerClipData
    {
        public T from, to;
        public EaseFunction ease;
        public Action<object> setter;

        public override bool Verify()
        {
            Debug.Assert(Lerp != null, "无效泛型参数:" + typeof(T).Name);
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new ValueClip<T>(this);

        public static readonly Func<T, T, float, T> Lerp = Lerpper.Lerp<T>();
        private static class Lerpper
        {
            private static class Impl<U> { public static Func<U, U, float, U> lerp; }
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
            public static Func<U, U, float, U> Lerp<U>() => Impl<U>.lerp;
        }
    }
    private class ValueClip<T> : RunnerClip<ValueClipData<T>>
    {
        public ValueClip(ValueClipData<T> data) : base(data) { }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

            var t = Mathf.Clamp01(Elapsed / Data.duration);
            var et = Data.ease != null ? Data.ease(t) : t;

            var value = ValueClipData<T>.Lerp(Data.from, Data.to, et);
            invoker.Invoke(progress, () => Data.setter(value));
            return subProgress;
        }
    }

    [Serializable]
    public class InvokerClipData : RunnerClipData
    {
        public Action invoke;

        public override bool Verify()
        {
            speed = 1;
            return base.Verify();
        }
        public override RunnerClip CreateClip() => new InvokerClip(this);
    }
    private class InvokerClip : RunnerClip<InvokerClipData>
    {
        public InvokerClip(InvokerClipData data) : base(data) { }
        protected override float Running(RunnerGraph graph, float deltaTime, float progress, IInvoker invoker)
        {
            var subProgress = base.Running(graph, deltaTime, progress, invoker);
            if (subProgress < 0) return subProgress;

            invoker.Invoke(progress, Data.invoke);
            return subProgress;
        }
    }
}