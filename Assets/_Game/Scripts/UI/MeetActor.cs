using UnityEngine;

namespace KartCup
{
    /// One of the drivers standing about with their kart on the title's street (MenuMeet builds them). Loops its own
    /// clip (standing, wiping the bodywork, sitting on the kart), breaks into one of the others now and then, and
    /// turns its head to the camera as it tracks past: all the time, or in glances up from what it is doing.
    public sealed class MeetActor : MonoBehaviour
    {
        [SerializeField] Animation anim;
        [SerializeField] string[] fidgets;                  // takes played once, at random, between loops of the main clip
        [SerializeField] Vector2 every = new Vector2(4f, 9f);
        [SerializeField] Transform head;
        [SerializeField] float maxYaw = 70f, maxPitch = 22f;
        [SerializeField] bool glances;                      // busy: a look up at the camera, then back to the job
        [SerializeField] Quaternion rest = Quaternion.identity;   // the head bone as the rig holds it, kept by Bind()
        Transform cam;
        Vector2 look;
        float nextFidget, nextGlance;
        bool watching = true;

#if UNITY_EDITOR
        public void Bind(Animation a, Transform headBone, string[] others, bool busy)
        {
            anim = a; head = headBone; fidgets = others; glances = busy;
            if (head != null) rest = head.localRotation;
        }

        /// Previews: LateUpdate never runs in edit mode. The head as the title has it, the camera standing at `camera`.
        public void FaceInEditor(Transform camera) { cam = camera; if (head == null) return; look = Want(); Turn(); }
#endif

        void Start()
        {
            cam = Camera.main != null ? Camera.main.transform : null;
            nextFidget = Time.time + Random.Range(every.x, every.y);     // out of step with each other
            if (anim != null && anim.clip != null) anim[anim.clip.name].time = Random.Range(0f, anim.clip.length);
        }

        /// A take now: the driver the select screen just arrived at reacts.
        public void Fidget() { nextFidget = Time.time; }

        void Update()
        {
            if (anim == null || fidgets == null || fidgets.Length == 0 || Time.time < nextFidget) return;
            nextFidget = Time.time + Random.Range(every.x, every.y);
            anim.CrossFade(fidgets[Random.Range(0, fidgets.Length)], 0.2f);
            anim.CrossFadeQueued(anim.clip.name, 0.25f, QueueMode.CompleteOthers);
        }

        void LateUpdate()
        {
            if (head == null || cam == null) return;
            if (glances && Time.time >= nextGlance)
            {
                watching = !watching;
                nextGlance = Time.time + (watching ? Random.Range(1.6f, 3.2f) : Random.Range(3f, 6f));
            }
            look = Vector2.Lerp(look, watching ? Want() : Vector2.zero, 1f - Mathf.Exp(-4f * Time.deltaTime));
            Turn();
        }

        /// The turn (yaw, pitch) that puts the face on the camera, as far as a neck goes.
        Vector2 Want()
        {
            var d = transform.InverseTransformDirection(cam.position - head.position);    // the driver faces its own +Z
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            // a camera round the back is not craned at: the head goes back to the front
            if (Mathf.Abs(yaw) > maxYaw + 30f) return Vector2.zero;
            return new Vector2(Mathf.Clamp(yaw, -maxYaw, maxYaw), Mathf.Clamp(pitch, -maxPitch, maxPitch));
        }

        void Turn()
        {
            // Set from the rest pose, never composed onto what is there: the clips leave the head bone alone, and a
            // turn read back next frame would compound (BlockyDriverPose has the story).
            var turn = transform.rotation * Quaternion.Euler(look.y, look.x, 0f) * Quaternion.Inverse(transform.rotation);
            head.rotation = turn * head.parent.rotation * rest;
        }
    }
}
