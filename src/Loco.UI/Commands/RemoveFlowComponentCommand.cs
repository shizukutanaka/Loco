using System;
using System.Collections.Generic;
using System.Linq;
using Loco.Core.Commands;
using Loco.Core.FlowComposer;
using Loco.Core.Models;
using Loco.UI.Controls;

namespace Loco.UI.Commands
{
    /// <summary>
    /// Command to remove a flow component
    /// </summary>
    public class RemoveFlowComponentCommand : CommandBase
    {
        private readonly List<FlowComponent> _components;
        private readonly FlowComponent _component;
        private readonly FlowBuilder _flowBuilder;
        private readonly Action _updatePreviewAction;
        private int _removedIndex;
        
        public RemoveFlowComponentCommand(
            List<FlowComponent> components,
            FlowComponent component,
            FlowBuilder flowBuilder,
            Action updatePreviewAction)
            : base($"Remove {component?.Type} Component")
        {
            _components = components ?? throw new ArgumentNullException(nameof(components));
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _flowBuilder = flowBuilder ?? throw new ArgumentNullException(nameof(flowBuilder));
            _updatePreviewAction = updatePreviewAction ?? throw new ArgumentNullException(nameof(updatePreviewAction));
        }
        
        public override void Execute()
        {
            _removedIndex = _components.IndexOf(_component);
            _components.Remove(_component);
            
            // Rebuild the flow without the removed component
            RebuildFlow();
            _updatePreviewAction();
        }
        
        public override void Undo()
        {
            // Restore the component at its original position
            if (_removedIndex >= 0 && _removedIndex <= _components.Count)
            {
                _components.Insert(_removedIndex, _component);
            }
            else
            {
                _components.Add(_component);
            }
            
            // Rebuild the flow with the restored component
            RebuildFlow();
            _updatePreviewAction();
        }
        
        private void RebuildFlow()
        {
            _flowBuilder.Clear();
            foreach (var component in _components)
            {
                switch (component.Type)
                {
                    case ComponentType.Trigger:
                        _flowBuilder.AddTrigger(component.ComponentId, component.Parameters);
                        break;
                    case ComponentType.Condition:
                        _flowBuilder.AddCondition(component.ComponentId, component.Parameters);
                        break;
                    case ComponentType.Action:
                        _flowBuilder.AddAction(component.ComponentId, component.Parameters);
                        break;
                }
            }
        }
    }
}
