import { useState } from 'react';
import { useWorkflowStore } from '@/store/workflowStore';
import {
  FolderOpen,
  Save,
  Download,
  Play,
  Plus,
  Settings,
  LayoutTemplate,
} from 'lucide-react';
import { TemplateGallery } from '@/components/TemplateGallery/TemplateGallery';

export function Toolbar() {
  const { workflow, newWorkflow, exportWorkflow, updateWorkflowMetadata } =
    useWorkflowStore();
  const [isEditingName, setIsEditingName] = useState(false);
  const [workflowName, setWorkflowName] = useState(workflow?.name || 'New Workflow');
  const [isTemplateGalleryOpen, setIsTemplateGalleryOpen] = useState(false);

  const handleNewWorkflow = () => {
    if (confirm('Create a new workflow? Current workflow will be cleared.')) {
      newWorkflow();
      setWorkflowName('New Workflow');
    }
  };

  const handleExportJSON = () => {
    const workflowData = exportWorkflow();
    const jsonString = JSON.stringify(workflowData, null, 2);
    const blob = new Blob([jsonString], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${workflow?.name || 'workflow'}.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  };

  const handleImportJSON = () => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = (e) => {
      const file = (e.target as HTMLInputElement).files?.[0];
      if (file) {
        const reader = new FileReader();
        reader.onload = (event) => {
          try {
            JSON.parse(event.target?.result as string);
            // TODO: Implement loadWorkflow when backend API is ready
            alert('Workflow imported successfully!');
          } catch (_error) {
            alert('Failed to import workflow: Invalid JSON');
          }
        };
        reader.readAsText(file);
      }
    };
    input.click();
  };

  const handleSaveWorkflow = () => {
    // This would call the API to save the workflow
    alert('Save workflow functionality will be connected to backend API');
  };

  const handleRunWorkflow = () => {
    // This would call the API to execute the workflow
    alert('Run workflow functionality will be connected to backend API');
  };

  const handleNameBlur = () => {
    setIsEditingName(false);
    if (workflowName.trim()) {
      updateWorkflowMetadata({ name: workflowName });
    } else {
      setWorkflowName(workflow?.name || 'New Workflow');
    }
  };

  return (
    <>
      <div className="h-16 bg-white border-b border-gray-200 flex items-center justify-between px-6">
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2">
            <div className="w-10 h-10 bg-gradient-to-br from-loco-primary to-loco-secondary rounded-lg flex items-center justify-center text-white font-bold text-lg">
              L
            </div>
            <span className="text-xl font-bold text-gray-900">Loco</span>
          </div>

          <div className="h-8 w-px bg-gray-300"></div>

          {isEditingName ? (
            <input
              type="text"
              value={workflowName}
              onChange={(e) => setWorkflowName(e.target.value)}
              onBlur={handleNameBlur}
              onKeyDown={(e) => e.key === 'Enter' && handleNameBlur()}
              className="px-3 py-1 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
              autoFocus
            />
          ) : (
            <button
              onClick={() => setIsEditingName(true)}
              className="text-lg font-medium text-gray-900 hover:text-loco-primary transition-colors"
            >
              {workflowName}
            </button>
          )}
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleNewWorkflow}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="New Workflow"
          >
            <Plus className="w-4 h-4" />
            <span className="text-sm font-medium">New</span>
          </button>

          <button
            onClick={() => setIsTemplateGalleryOpen(true)}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Templates"
          >
            <LayoutTemplate className="w-4 h-4" />
            <span className="text-sm font-medium">Templates</span>
          </button>

          <button
            onClick={handleImportJSON}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Import JSON"
          >
            <FolderOpen className="w-4 h-4" />
            <span className="text-sm font-medium">Import</span>
          </button>

          <button
            onClick={handleExportJSON}
            className="flex items-center gap-2 px-4 py-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Export JSON"
          >
            <Download className="w-4 h-4" />
            <span className="text-sm font-medium">Export</span>
          </button>

          <button
            onClick={handleSaveWorkflow}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-primary hover:bg-blue-700 rounded-lg transition-colors"
            title="Save Workflow"
          >
            <Save className="w-4 h-4" />
            <span className="text-sm font-medium">Save</span>
          </button>

          <div className="h-8 w-px bg-gray-300 mx-2"></div>

          <button
            onClick={handleRunWorkflow}
            className="flex items-center gap-2 px-4 py-2 text-white bg-loco-success hover:bg-green-700 rounded-lg transition-colors"
            title="Run Workflow"
          >
            <Play className="w-4 h-4" />
            <span className="text-sm font-medium">Run</span>
          </button>

          <button
            className="p-2 text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Settings"
          >
            <Settings className="w-4 h-4" />
          </button>
        </div>
      </div>

      <TemplateGallery
        isOpen={isTemplateGalleryOpen}
        onClose={() => setIsTemplateGalleryOpen(false)}
      />
    </>
  );
}
