using RuntimeInspectorNamespace;
using UnityEngine;

namespace DevOps
{
    public class KinematicControllersUI : MonoBehaviour
    {
#if DEV_GAME_ENVIRONMENT || UNITY_EDITOR || !DISABLE_SRDEBUGGER
        public PointerEventListener leftRotate;
        public PointerEventListener rightRotate;

        public PointerEventListener leftMove;
        public PointerEventListener rightMove;
        public PointerEventListener forwardMove;
        public PointerEventListener backwardMove;
        public PointerEventListener upMove;
        public PointerEventListener downMove;

        public PointerEventListener speedUp;
        public PointerEventListener speedDown;
#endif
    }
}