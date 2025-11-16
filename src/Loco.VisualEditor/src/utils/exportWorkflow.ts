/**
 * Workflow Export Utilities
 *
 * Provides utilities for exporting workflows to various formats.
 */

import type { Workflow } from '@/types/workflow';

/**
 * Export workflow as JSON file
 * Creates a downloadable JSON file with the workflow data
 *
 * @param workflow - Workflow object to export
 */
export function exportWorkflowAsJson(workflow: Workflow): void {
  const jsonString = JSON.stringify(workflow, null, 2);
  const blob = new Blob([jsonString], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${workflow.name || 'workflow'}.json`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Export workflow as CSV (nodes and edges)
 * Creates a downloadable CSV file with workflow structure
 *
 * @param workflow - Workflow object to export
 */
export function exportWorkflowAsCSV(workflow: Workflow): void {
  // Build CSV content for nodes
  let csvContent = 'Nodes\n';
  csvContent += 'id,label,type,position_x,position_y\n';
  workflow.nodes.forEach((node) => {
    csvContent += `"${node.id}","${node.data.label}","${node.type}","${node.position?.x || 0}","${node.position?.y || 0}"\n`;
  });

  // Add edges section
  csvContent += '\nEdges\n';
  csvContent += 'source,target,label,condition\n';
  workflow.edges.forEach((edge) => {
    const label = edge.data?.label || '';
    const condition = edge.data?.condition || '';
    csvContent += `"${edge.source}","${edge.target}","${label}","${condition}"\n`;
  });

  // Download CSV file
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `${workflow.name || 'workflow'}.csv`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Copy workflow JSON to clipboard
 * Copies workflow data as JSON string to clipboard
 *
 * @param workflow - Workflow object to copy
 * @returns Promise that resolves when copy is complete
 */
export async function copyWorkflowToClipboard(workflow: Workflow): Promise<void> {
  const jsonString = JSON.stringify(workflow, null, 2);
  try {
    await navigator.clipboard.writeText(jsonString);
  } catch (error) {
    console.error('Failed to copy workflow to clipboard:', error);
    throw new Error('Failed to copy workflow to clipboard');
  }
}
