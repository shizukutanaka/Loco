import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  exportWorkflowAsJson,
  exportWorkflowAsCSV,
  copyWorkflowToClipboard,
} from './exportWorkflow';
import type { Workflow } from '@/types/workflow';

const workflow: Workflow = {
  id: 'wf-1',
  name: 'My Flow',
  description: '',
  nodes: [
    { id: 'n1', type: 'trigger', position: { x: 10, y: 20 }, data: { label: 'Start', config: {} } },
    { id: 'n2', type: 'action', position: { x: 30, y: 40 }, data: { label: 'Do it', config: {} } },
  ],
  edges: [
    { id: 'e1', source: 'n1', target: 'n2', data: { label: 'go', condition: 'success' } },
  ],
  metadata: { version: '1.0', isPublic: false },
  createdAt: '2026-01-01T00:00:00.000Z',
  updatedAt: '2026-01-01T00:00:00.000Z',
};

describe('exportWorkflow', () => {
  // jsdom's Blob has no .text(), so capture the content the code passes in
  let blobContent: string;
  let blobType: string;
  let clickedLink: HTMLAnchorElement | null;

  beforeEach(() => {
    blobContent = '';
    blobType = '';
    clickedLink = null;

    vi.stubGlobal(
      'Blob',
      class {
        constructor(parts: unknown[], opts?: { type?: string }) {
          blobContent = (parts ?? []).map(String).join('');
          blobType = opts?.type ?? '';
        }
      }
    );

    // jsdom does not implement object URLs
    (URL as unknown as { createObjectURL: unknown }).createObjectURL = vi.fn(() => 'blob:mock');
    (URL as unknown as { revokeObjectURL: unknown }).revokeObjectURL = vi.fn();

    // Capture the anchor whose click() triggers the download
    const realCreate = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
      const el = realCreate(tag) as HTMLElement;
      if (tag === 'a') {
        clickedLink = el as HTMLAnchorElement;
        vi.spyOn(el as HTMLAnchorElement, 'click').mockImplementation(() => {});
      }
      return el;
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('exportWorkflowAsJson downloads a .json file named after the workflow', () => {
    exportWorkflowAsJson(workflow);

    expect(clickedLink).not.toBeNull();
    expect(clickedLink!.download).toBe('My Flow.json');
    expect(URL.createObjectURL).toHaveBeenCalledOnce();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:mock');
    expect(document.body.contains(clickedLink)).toBe(false); // cleaned up

    expect(JSON.parse(blobContent).id).toBe('wf-1');
    expect(blobType).toBe('application/json');
  });

  it('falls back to "workflow" as the filename when name is empty', () => {
    exportWorkflowAsJson({ ...workflow, name: '' });
    expect(clickedLink!.download).toBe('workflow.json');
  });

  it('exportWorkflowAsCSV emits a Nodes section and an Edges section', () => {
    exportWorkflowAsCSV(workflow);

    expect(clickedLink!.download).toBe('My Flow.csv');
    expect(blobContent).toContain('Nodes');
    expect(blobContent).toContain('id,label,type,position_x,position_y');
    expect(blobContent).toContain('"n1","Start","trigger","10","20"');
    expect(blobContent).toContain('Edges');
    expect(blobContent).toContain('source,target,label,condition');
    expect(blobContent).toContain('"n1","n2","go","success"');
  });

  it('copyWorkflowToClipboard writes the JSON string to the clipboard', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });

    await copyWorkflowToClipboard(workflow);

    expect(writeText).toHaveBeenCalledOnce();
    expect(JSON.parse(writeText.mock.calls[0][0]).name).toBe('My Flow');
  });

  it('copyWorkflowToClipboard rejects with a clear error when the clipboard fails', async () => {
    const writeText = vi.fn().mockRejectedValue(new Error('denied'));
    Object.assign(navigator, { clipboard: { writeText } });
    vi.spyOn(console, 'error').mockImplementation(() => {});

    await expect(copyWorkflowToClipboard(workflow)).rejects.toThrow(
      'Failed to copy workflow to clipboard'
    );
  });
});
