using UnityEngine;

namespace _Game.Scripts.Utils.VContainer
{
    public interface IInjectableBehaviour
    {
        public System.Type SubscribedLifeTimeScopeType { get; }
    }
}