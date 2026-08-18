#if UNITY_EDITOR
using System.Collections.Generic;

namespace PschLib.StateMachines
{
    public interface IStateMachineDebugInfo
    {
        string StateTypeName { get; }
        string CurrentStateName { get; }
        bool IsStarted { get; }
        void GetRegisteredStateNames(List<string> results);
    }
}
#endif
