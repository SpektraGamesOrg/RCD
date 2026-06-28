using System;
using UnityEngine;

namespace _Game.Scripts.Utils.VContainer
{
    public abstract class InjectableMonoBehaviourBase<T> : MonoBehaviour, IInjectableBehaviour
        where T : LifetimeScopeWrapper<T>
    {
        private Type _subscribedLifeTimeScopeType = null;
        Type IInjectableBehaviour.SubscribedLifeTimeScopeType
        {
            get
            {
                if(_subscribedLifeTimeScopeType == null)
                    _subscribedLifeTimeScopeType = typeof(T);
                
                return _subscribedLifeTimeScopeType;
            }
        }

        protected virtual void Awake()
        {
            InjectHelper.Inject(this, this);
        }

        protected virtual void OnDestroy()
        {
            InjectHelper.Deinject(this, this);
        }
    }
}