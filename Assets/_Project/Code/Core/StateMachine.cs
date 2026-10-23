using UnityEngine;
using System;
using System.Collections.Generic;

// ============================================================
//  StateMachine<TState> — Generic finite state machine
//  Usage: var sm = new StateMachine<ICharacterState>();
//         sm.AddState(new IdleState());
//         sm.SetState<IdleState>();
// ============================================================
namespace EchoesOfTheLost.Core
{
    /// <summary>Interface all states must implement.</summary>
    public interface IState
    {
        void Enter();
        void Update();
        void FixedUpdate();
        void Exit();
    }

    public class StateMachine
    {
        private IState _currentState;
        private Dictionary<Type, IState> _states = new Dictionary<Type, IState>();

        public IState CurrentState => _currentState;
        public event Action<IState, IState> OnStateChanged; // (from, to)

        /// <summary>Register a state with the machine.</summary>
        public void AddState(IState state) => _states[state.GetType()] = state;

        /// <summary>Transition to a state by type.</summary>
        public void SetState<T>() where T : IState
        {
            if (!_states.TryGetValue(typeof(T), out var newState))
            {
                Debug.LogError($"[StateMachine] State not registered: {typeof(T).Name}");
                return;
            }
            TransitionTo(newState);
        }

        /// <summary>Force transition to a specific state instance.</summary>
        public void SetState(IState newState)
        {
            TransitionTo(newState);
        }

        private void TransitionTo(IState newState)
        {
            if (newState == _currentState) return;

            var prev = _currentState;
            _currentState?.Exit();
            _currentState = newState;
            _currentState.Enter();
            OnStateChanged?.Invoke(prev, _currentState);
        }

        public void Update() => _currentState?.Update();
        public void FixedUpdate() => _currentState?.FixedUpdate();

        public bool IsInState<T>() where T : IState
            => _currentState is T;
    }
}
