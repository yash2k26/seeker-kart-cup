using UnityEngine;

namespace KartCup
{
    /// A rocket that hits something, or a bomb that is driven into, goes off: Powerslide only destroys the object, so
    /// this leaves the effect (flash, sparks, the bang) where it was. Not when the scene itself is being torn down.
    public sealed class BurstOnDestroy : MonoBehaviour
    {
        public GameObject fx;
        bool quitting;

        void OnApplicationQuit() => quitting = true;

        void OnDestroy()
        {
            if (quitting || fx == null || !gameObject.scene.isLoaded) return;
            Instantiate(fx, transform.position, Quaternion.identity);
        }
    }
}
