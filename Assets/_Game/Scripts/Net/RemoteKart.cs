using System.Collections.Generic;
using PowerslideKartPhysics;
using TMPro;
using UnityEngine;

namespace KartCup
{
    /// Another phone's kart on this phone: the same prefab with its physics switched off, replayed ~150 ms behind
    /// the sender's clock so there is always a next sample to slide toward. It keeps its colliders (you can bump
    /// into it) but nothing here moves it except the network. Wheels and the driver's arms are animated from the
    /// received state so it reads as driven, not dragged.
    public sealed class RemoteKart : MonoBehaviour
    {
        public string Id { get; private set; }
        public string PlayerName { get; private set; }

        const float Delay = 0.15f;          // interpolation buffer, in the sender's race clock
        const float MaxExtrapolate = 0.25f;

        struct Snap { public float t; public Vector3 p; public Quaternion r; public Vector3 v; public float steer; }
        readonly List<Snap> buf = new(32);

        Kart kart;
        BlockyDriverPose pose;
        KartProgress progress;
        Transform[] wheelTr, wheelVisual; float[] wheelSide, wheelAngle, wheelRadius; bool[] wheelFront;
        TextMeshPro tag;
        Vector3 lastPos; Quaternion lastRot = Quaternion.identity; float lastSpeed;

        public void Bind(Kart k, BlockyDriverPose driverPose, string id, string playerName)
        {
            Id = id; PlayerName = playerName;
            kart = k; pose = driverPose;
            progress = GetComponent<KartProgress>();
            if (progress != null) progress.remote = true;

            kart.enabled = false;                           // no forces, no input, no drift logic
            kart.rb.isKinematic = true;                     // moved by hand; still a collider the local kart can hit
            kart.rb.interpolation = RigidbodyInterpolation.None;
            foreach (var input in GetComponents<KartInput>()) Destroy(input);

            var wheels = k.GetComponentsInChildren<KartWheel>();
            wheelTr = new Transform[wheels.Length]; wheelVisual = new Transform[wheels.Length];
            wheelSide = new float[wheels.Length]; wheelAngle = new float[wheels.Length]; wheelRadius = new float[wheels.Length]; wheelFront = new bool[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                wheels[i].enabled = false;                  // its Update would keep extending the suspension of a kart that never lands
                wheelTr[i] = wheels[i].transform;
                wheelVisual[i] = wheels[i].visualWheel != null ? wheels[i].visualWheel : wheels[i].transform.Find("Visual Mesh");
                wheelSide[i] = wheelTr[i].localPosition.x > -0.01f ? 1f : -1f;
                wheelRadius[i] = Mathf.Max(0.05f, wheels[i].radius);
                wheelFront[i] = wheels[i].name.Contains("F");
                if (wheelVisual[i] != null) wheelVisual[i].localPosition = Vector3.zero;
            }
            foreach (var audio in GetComponentsInChildren<KartAudio>()) audio.enabled = false;   // its engine follows a kart that is not simulated
            lastPos = transform.position; lastRot = kart.rotator != null ? kart.rotator.rotation : transform.rotation;
            NameTag();
        }

        void NameTag()
        {
            var go = new GameObject("Tag");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 2.1f;
            tag = go.AddComponent<TextMeshPro>();
            tag.text = PlayerName; tag.fontSize = 3.2f; tag.alignment = TextAlignmentOptions.Center;
            tag.color = new Color(0.957f, 0.945f, 0.918f); tag.outlineWidth = 0.25f; tag.outlineColor = new Color(0.04f, 0.08f, 0.11f);
            tag.rectTransform.sizeDelta = new Vector2(6f, 1f);
            tag.sortingOrder = 10;
        }

        /// A state sample from the network. `t` is the SENDER's race clock in seconds.
        public void Push(Vector3 p, Quaternion r, Vector3 v, float steer, float t, int lap, int checkpoint, float score)
        {
            if (buf.Count > 0 && t <= buf[^1].t) return;   // out of order or duplicate
            buf.Add(new Snap { t = t, p = p, r = r, v = v, steer = steer });
            if (buf.Count > 30) buf.RemoveAt(0);
            if (progress != null) { progress.lap = lap; progress.checkpointIndex = checkpoint; progress.score = score; }
        }

        void Update()
        {
            if (buf.Count > 0)
            {
                float now = NetRace.RaceSeconds - Delay;
                Sample(now, out var p, out var r, out var steer);
                lastSpeed = Vector3.Distance(p, lastPos) / Mathf.Max(Time.deltaTime, 1e-4f);
                lastPos = p; lastRot = r;
                transform.position = p;
                if (kart.rotator != null) kart.rotator.rotation = r;
                if (pose != null) pose.Steer = steer;
                Wheels(steer, Time.deltaTime);
            }
            if (tag != null && Camera.main != null)
            {
                tag.transform.rotation = Quaternion.LookRotation(tag.transform.position - Camera.main.transform.position);
                float d = Vector3.Distance(tag.transform.position, Camera.main.transform.position);
                tag.enabled = d < 90f;
            }
        }

        void Sample(float now, out Vector3 p, out Quaternion r, out float steer)
        {
            var last = buf[^1];
            if (now >= last.t)
            {
                float dt = Mathf.Min(now - last.t, MaxExtrapolate);   // ahead of the newest sample: coast on its velocity, then hold
                p = last.p + last.v * dt; r = last.r; steer = last.steer;
                return;
            }
            var first = buf[0];
            if (now <= first.t) { p = first.p; r = first.r; steer = first.steer; return; }
            for (int i = buf.Count - 2; i >= 0; i--)
            {
                var a = buf[i]; var b = buf[i + 1];
                if (now < a.t) continue;
                float u = Mathf.InverseLerp(a.t, b.t, now);
                p = Vector3.LerpUnclamped(a.p, b.p, u);
                r = Quaternion.Slerp(a.r, b.r, u);
                steer = Mathf.Lerp(a.steer, b.steer, u);
                return;
            }
            p = last.p; r = last.r; steer = last.steer;
        }

        /// The kit's own wheel maths with the compression term dropped: forward = axle, up = rolling angle.
        void Wheels(float steer, float dt)
        {
            if (wheelTr == null) return;
            float forward = Vector3.Dot(lastRot * Vector3.forward, (lastRot * Vector3.forward) * lastSpeed);
            for (int i = 0; i < wheelTr.Length; i++)
            {
                if (wheelVisual[i] == null) continue;
                float rate = forward / (wheelRadius[i] * 2f * Mathf.PI) * 360f;
                wheelAngle[i] = Mathf.Repeat(wheelAngle[i] + rate * dt, 360f);
                var tr = wheelTr[i];
                Vector3 axle = tr.right * wheelSide[i] + (wheelFront[i] ? tr.forward * (steer * wheelSide[i] * -0.55f) : Vector3.zero);
                float a = wheelAngle[i] * Mathf.Deg2Rad * wheelSide[i];
                wheelVisual[i].rotation = Quaternion.LookRotation(axle, tr.TransformDirection(0f, Mathf.Sin(a), Mathf.Cos(a)));
            }
        }
    }
}
