using System;
using System.Collections.Generic;
using Loco.Core.Commands;
using Loco.Core.FlowComposer;
using Loco.Core.Models;
using Loco.UI.Controls;

namespace Loco.UI.Commands
{
    /// <summary>
    /// Command to add a flow component
    /// </summary>
    public class AddFlowComponentCommand : CommandBase
    {
        private readonly List<FlowComponent> _components;
        private readonly FlowComponent _component;
        private readonly FlowBuilder _flowBuilder;
        private readonly Action _updatePreviewAction;
        
        public AddFlowComponentCommand(
            List<FlowComponent> components,
            FlowComponent component,
            FlowBuilder flowBuilder,
            Action updatePreviewAction)
            : base($"Add {component?.Type} Component")
        {
            _components = components ?? throw new ArgumentNullException(nameof(components));
            _component = component ?? throw new ArgumentNullException(nameof(component));
            _flowBuilder = flowBuilder ?? throw new ArgumentNullException(nameof(flowBuilder));
            _updatePreviewAction = updatePreviewAction ?? throw new ArgumentNullException(nameof(updatePreviewAction));
        }
        
        public override void Execute()
        {
            _components.Add(_component);
            
            // Add to the actual flow builder
            switch (_component.Type)
            {
                case ComponentType.Trigger:
                    _flowBuilder.AddTrigger(_component.ComponentId, _component.Parameters);
                    break;
                case ComponentType.Condition:
                    _flowBuilder.AddCondition(_component.ComponentId, _component.Parameters);
                    break;
                case ComponentType.Action:
                    _flowBuilder.AddAction(_component.ComponentId, _component.Parameters);
                    break;
            }
            
            _updatePreviewAction();
        }
        
        public override void Undo()
        {
            _components.Remove(_component);
            
            // Rebuild the flow from the modified component list
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
            
            _updatePreviewAction();
        }
    }
}
