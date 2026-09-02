import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { AIAssistant, topicFor } from './AIAssistant';
import { useWorkflowStore } from '@/store/workflowStore';
import { ToastProvider } from '@/contexts/ToastContext';

/**
 * What this panel can and cannot do.
 *
 * There is no model behind it. `aiAnalyzer.ts` is 563 lines of static checks
 * over the workflow graph and makes no network call of any kind, and the text
 * box matches eleven keywords across four topics.
 *
 * That is a reasonable feature. The defect was what happened when the keywords
 * did not match: asking "why is my Slack node failing?" replied "I found 7
 * insights about your workflow. Here are the most important ones:" and listed
 * them - the generic output, worded as an answer to a question the panel had
 * not understood. These tests pin that an unmatched question is now told so,
 * and that the four topics are reachable without guessing a keyword.
 */

const workflow = () => {
  useWorkflowStore.setState({
    nodes: [
      { id: 'trigger-1', type: 'trigger', position: { x: 0, y: 0 }, data: { label: 'Start', config: {} } },
      {
        id: 'a1',
        type: 'action',
        position: { x: 200, y: 0 },
        data: { label: 'Call API', integration: 'http', config: { action: 'get', parameters: { url: 'https://x.test' } } },
      },
    ],
    edges: [{ id: 'e1', source: 'trigger-1', target: 'a1' }],
    selectedNodeId: null,
  });
};

/**
 * Renders the panel and waits for its opening analysis to land.
 *
 * Opening runs an analysis that REPLACES the message list, so anything sent
 * before it settles is discarded. Waiting first keeps these tests about the
 * behaviour under test rather than about that race.
 *
 * useToast() throws outside a ToastProvider, so the panel needs one.
 */
const show = async () => {
  render(
    <ToastProvider>
      <AIAssistant isOpen onClose={vi.fn()} />
    </ToastProvider>
  );

  await waitFor(() => expect(screen.getByText(/I've analyzed your workflow/i)).toBeTruthy());
};

const ask = async (text: string) => {
  fireEvent.change(screen.getByLabelText(/ask about workflow/i), { target: { value: text } });
  fireEvent.click(screen.getByTitle('Send'));
};

describe('topicFor', () => {
  it.each([
    ['how do I improve performance?', 'performance'],
    ['is this a security risk?', 'security'],
    ['what errors are there', 'error_fix'],
    ['show me patterns', 'pattern'],
  ])('routes %s', (question, expected) => {
    expect(topicFor(question)).toBe(expected);
  });

  it('returns null for a question it cannot answer', () => {
    // The whole point: this used to be indistinguishable from a match, because
    // the caller treated "no match" as "give the generic list".
    expect(topicFor('why is my Slack node failing?')).toBeNull();
    expect(topicFor('what does this workflow do')).toBeNull();
  });

  it('is narrower than it looks, and says so rather than guessing', () => {
    // "is this secure?" does not match: the keyword is "security". Worth
    // pinning, because it is the kind of question a user would expect to work
    // and the honest answer is to admit it was not understood.
    expect(topicFor('is this secure?')).toBeNull();
  });

  it('is case-insensitive', () => {
    expect(topicFor('SECURITY')).toBe('security');
  });
});

describe('AIAssistant', () => {
  beforeEach(() => {
    workflow();
  });

  it('says plainly when it does not understand a question', async () => {
    await show();
    await ask('why is my Slack node failing?');

    await waitFor(() => {
      expect(screen.getByText(/can't answer questions in general/i)).toBeTruthy();
    });

    // And must NOT present the generic list as if it were the answer.
    expect(screen.queryByText(/Here are the most important ones/i)).toBeNull();
  });

  it('names the topics it can answer on when it does not understand', async () => {
    await show();
    await ask('explain this workflow to me');

    await waitFor(() => {
      const message = screen.getByText(/can't answer questions in general/i).textContent ?? '';
      for (const topic of ['performance', 'security', 'errors', 'patterns']) {
        expect(message.toLowerCase(), `does not mention ${topic}`).toContain(topic);
      }
    });
  });

  it('offers every topic as a button, so no keyword has to be guessed', async () => {
    await show();

    for (const label of ['Performance', 'Security', 'Errors', 'Patterns']) {
      expect(screen.getByRole('button', { name: label }), `no ${label} button`).toBeTruthy();
    }
  });

  it('answers a topic button without claiming not to understand it', async () => {
    await show();
    fireEvent.click(screen.getByRole('button', { name: 'Security' }));

    await waitFor(() => {
      expect(screen.getAllByText('Security').length).toBeGreaterThan(1);
    });

    expect(screen.queryByText(/can't answer questions in general/i)).toBeNull();
  });

  it('says it found nothing rather than staying silent', async () => {
    await show();
    fireEvent.click(screen.getByRole('button', { name: 'Patterns' }));

    await waitFor(() => {
      // Either it found patterns, or it says it found none. What it must not
      // do is answer a topic with nothing at all.
      const foundNone = screen.queryByText(/found none/i);
      const foundSome = screen.queryByText(/patterns I detected/i);
      expect(Boolean(foundNone || foundSome)).toBe(true);
    });
  });
});
