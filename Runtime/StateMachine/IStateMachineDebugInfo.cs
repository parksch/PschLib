#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace PschLib.StateMachines
{
    public interface IStateMachineDebugInfo
    {
        event Action DebugStateChanged;
        string StateTypeName { get; }
        string CurrentStateName { get; }
        bool IsStarted { get; }
        void GetRegisteredStateNames(List<string> results);
    }
}
#endif
