/**
 * Node Plugin Manager Component
 *
 * Provides custom node development and plugin management:
 * - Browse plugin marketplace
 * - Install/uninstall plugins
 * - Enable/disable installed plugins
 * - View plugin SDK documentation
 * - Custom node creation wizard
 */

import { useState, useEffect } from 'react';
import {
  X,
  Package,
  Download,
  Trash2,
  Power,
  PowerOff,
  Search,
  Filter,
  Code,
  Book,
  CheckCircle,
  AlertCircle,
  Star,
  ExternalLink,
} from 'lucide-react';
import { useToast } from '@/contexts/ToastContext';

// ============================================================================
// Types
// ============================================================================

interface NodePluginManagerProps {
  isOpen: boolean;
  onClose: () => void;
}

type PluginStatus = 'installed' | 'available' | 'updating';
type PluginCategory = 'all' | 'data' | 'integration' | 'transformation' | 'utility' | 'ai';

interface Plugin {
  id: string;
  name: string;
  version: string;
  author: string;
  description: string;
  category: PluginCategory;
  status: PluginStatus;
  enabled: boolean;
  downloads: number;
  rating: number;
  icon?: string;
  documentation?: string;
  repository?: string;
  nodes: PluginNode[];
}

interface PluginNode {
  id: string;
  name: string;
  type: string;
  description: string;
}

// ============================================================================
// Node Plugin Manager Component
// ============================================================================

export function NodePluginManager({ isOpen, onClose }: NodePluginManagerProps) {
  const [activeTab, setActiveTab] = useState<'marketplace' | 'installed' | 'create'>('marketplace');
  const [plugins, setPlugins] = useState<Plugin[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<PluginCategory>('all');
  const [isLoading, setIsLoading] = useState(false);
  const toast = useToast();

  // Fetch plugins
  useEffect(() => {
    if (!isOpen) return;

    const fetchPlugins = async () => {
      setIsLoading(true);
      try {
        // TODO: Replace with actual API call
        // const response = await getPlugins();

        await new Promise((resolve) => setTimeout(resolve, 500));

        setPlugins([
          {
            id: 'plugin-1',
            name: 'AWS Services',
            version: '1.2.0',
            author: 'Loco Team',
            description: 'Complete AWS integration with S3, Lambda, DynamoDB, and more',
            category: 'integration',
            status: 'installed',
            enabled: true,
            downloads: 15420,
            rating: 4.8,
            documentation: 'https://docs.loco.dev/plugins/aws',
            repository: 'https://github.com/loco/plugin-aws',
            nodes: [
              { id: 'aws-s3', name: 'S3 Storage', type: 'integration', description: 'Upload/download files from S3' },
              { id: 'aws-lambda', name: 'Lambda Function', type: 'integration', description: 'Invoke AWS Lambda functions' },
              { id: 'aws-dynamodb', name: 'DynamoDB', type: 'integration', description: 'Query DynamoDB tables' },
            ],
          },
          {
            id: 'plugin-2',
            name: 'OpenAI GPT',
            version: '2.0.1',
            author: 'Loco Team',
            description: 'Integrate with OpenAI GPT models for text generation and analysis',
            category: 'ai',
            status: 'installed',
            enabled: true,
            downloads: 28931,
            rating: 4.9,
            documentation: 'https://docs.loco.dev/plugins/openai',
            repository: 'https://github.com/loco/plugin-openai',
            nodes: [
              { id: 'gpt-chat', name: 'GPT Chat', type: 'ai', description: 'Chat with GPT models' },
              { id: 'gpt-completion', name: 'Text Completion', type: 'ai', description: 'Generate text completions' },
            ],
          },
          {
            id: 'plugin-3',
            name: 'Database Connectors',
            version: '1.5.2',
            author: 'Community',
            description: 'Connect to PostgreSQL, MySQL, MongoDB, and more databases',
            category: 'data',
            status: 'available',
            enabled: false,
            downloads: 12054,
            rating: 4.6,
            documentation: 'https://docs.loco.dev/plugins/databases',
            repository: 'https://github.com/loco-community/plugin-databases',
            nodes: [
              { id: 'postgres', name: 'PostgreSQL', type: 'data', description: 'Query PostgreSQL databases' },
              { id: 'mysql', name: 'MySQL', type: 'data', description: 'Query MySQL databases' },
              { id: 'mongodb', name: 'MongoDB', type: 'data', description: 'Query MongoDB collections' },
            ],
          },
          {
            id: 'plugin-4',
            name: 'Data Transformers',
            version: '1.0.0',
            author: 'Community',
            description: 'Advanced data transformation utilities including JSON, XML, CSV processors',
            category: 'transformation',
            status: 'available',
            enabled: false,
            downloads: 8234,
            rating: 4.5,
            documentation: 'https://docs.loco.dev/plugins/transformers',
            repository: 'https://github.com/loco-community/plugin-transformers',
            nodes: [
              { id: 'json-transform', name: 'JSON Transform', type: 'transformation', description: 'Transform JSON data' },
              { id: 'xml-parser', name: 'XML Parser', type: 'transformation', description: 'Parse XML documents' },
              { id: 'csv-processor', name: 'CSV Processor', type: 'transformation', description: 'Process CSV files' },
            ],
          },
          {
            id: 'plugin-5',
            name: 'Slack Integration',
            version: '1.1.0',
            author: 'Loco Team',
            description: 'Send messages, create channels, and manage Slack workspaces',
            category: 'integration',
            status: 'available',
            enabled: false,
            downloads: 19567,
            rating: 4.7,
            documentation: 'https://docs.loco.dev/plugins/slack',
            repository: 'https://github.com/loco/plugin-slack',
            nodes: [
              { id: 'slack-message', name: 'Send Message', type: 'integration', description: 'Send Slack messages' },
              { id: 'slack-channel', name: 'Create Channel', type: 'integration', description: 'Create Slack channels' },
            ],
          },
        ]);
      } catch (error) {
        console.error('Failed to fetch plugins:', error);
        toast.error('Failed to load plugins');
      } finally {
        setIsLoading(false);
      }
    };

    fetchPlugins();
  }, [isOpen, toast]);

  const handleInstallPlugin = async (plugin: Plugin) => {
    try {
      // TODO: Call API to install plugin
      console.log('Installing plugin:', plugin.id);

      await new Promise((resolve) => setTimeout(resolve, 1500));

      setPlugins((prev) =>
        prev.map((p) =>
          p.id === plugin.id ? { ...p, status: 'installed' as PluginStatus, enabled: true } : p
        )
      );

      toast.success(`Plugin "${plugin.name}" installed successfully`);
    } catch (error) {
      console.error('Failed to install plugin:', error);
      toast.error('Failed to install plugin');
    }
  };

  const handleUninstallPlugin = async (plugin: Plugin) => {
    const confirmed = confirm(`Uninstall "${plugin.name}"? This will remove all nodes from this plugin.`);
    if (!confirmed) return;

    try {
      // TODO: Call API to uninstall plugin
      console.log('Uninstalling plugin:', plugin.id);

      await new Promise((resolve) => setTimeout(resolve, 1000));

      setPlugins((prev) =>
        prev.map((p) =>
          p.id === plugin.id ? { ...p, status: 'available' as PluginStatus, enabled: false } : p
        )
      );

      toast.success(`Plugin "${plugin.name}" uninstalled`);
    } catch (error) {
      console.error('Failed to uninstall plugin:', error);
      toast.error('Failed to uninstall plugin');
    }
  };

  const handleTogglePlugin = async (plugin: Plugin) => {
    try {
      // TODO: Call API to enable/disable plugin
      console.log('Toggling plugin:', plugin.id, !plugin.enabled);

      await new Promise((resolve) => setTimeout(resolve, 500));

      setPlugins((prev) =>
        prev.map((p) => (p.id === plugin.id ? { ...p, enabled: !p.enabled } : p))
      );

      toast.success(`Plugin "${plugin.name}" ${!plugin.enabled ? 'enabled' : 'disabled'}`);
    } catch (error) {
      console.error('Failed to toggle plugin:', error);
      toast.error('Failed to toggle plugin');
    }
  };

  const filteredPlugins = plugins.filter((plugin) => {
    const matchesSearch =
      plugin.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      plugin.description.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesCategory = selectedCategory === 'all' || plugin.category === selectedCategory;
    const matchesTab =
      activeTab === 'marketplace'
        ? true
        : activeTab === 'installed'
        ? plugin.status === 'installed'
        : false;

    return matchesSearch && matchesCategory && matchesTab;
  });

  const getCategoryColor = (category: PluginCategory) => {
    switch (category) {
      case 'data':
        return 'bg-blue-100 text-blue-700';
      case 'integration':
        return 'bg-purple-100 text-purple-700';
      case 'transformation':
        return 'bg-green-100 text-green-700';
      case 'utility':
        return 'bg-gray-100 text-gray-700';
      case 'ai':
        return 'bg-orange-100 text-orange-700';
      default:
        return 'bg-gray-100 text-gray-700';
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-6">
      <div className="bg-white rounded-xl shadow-2xl max-w-6xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="px-6 py-4 border-b border-gray-200 flex items-center justify-between">
          <div>
            <h2 className="text-xl font-bold text-gray-900">Plugin Manager</h2>
            <p className="text-sm text-gray-500 mt-1">Extend Loco with custom nodes and integrations</p>
          </div>
          <button
            onClick={onClose}
            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
            title="Close"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Tabs */}
        <div className="px-6 pt-4 border-b border-gray-200">
          <div className="flex gap-4">
            <button
              onClick={() => setActiveTab('marketplace')}
              className={`pb-3 px-2 text-sm font-medium border-b-2 transition-colors ${
                activeTab === 'marketplace'
                  ? 'border-loco-primary text-loco-primary'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              Marketplace
            </button>
            <button
              onClick={() => setActiveTab('installed')}
              className={`pb-3 px-2 text-sm font-medium border-b-2 transition-colors ${
                activeTab === 'installed'
                  ? 'border-loco-primary text-loco-primary'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              Installed ({plugins.filter((p) => p.status === 'installed').length})
            </button>
            <button
              onClick={() => setActiveTab('create')}
              className={`pb-3 px-2 text-sm font-medium border-b-2 transition-colors ${
                activeTab === 'create'
                  ? 'border-loco-primary text-loco-primary'
                  : 'border-transparent text-gray-500 hover:text-gray-700'
              }`}
            >
              Create Plugin
            </button>
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto">
          {activeTab === 'create' ? (
            // Create Plugin Tab
            <div className="p-6">
              <div className="max-w-3xl mx-auto space-y-6">
                <div className="text-center">
                  <Code className="w-16 h-16 text-loco-primary mx-auto mb-4" />
                  <h3 className="text-2xl font-bold text-gray-900 mb-2">Create Custom Nodes</h3>
                  <p className="text-gray-600">Build your own nodes and share them with the community</p>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="p-6 bg-blue-50 border border-blue-200 rounded-lg">
                    <Book className="w-8 h-8 text-blue-600 mb-3" />
                    <h4 className="text-lg font-semibold text-gray-900 mb-2">Plugin SDK Documentation</h4>
                    <p className="text-sm text-gray-700 mb-4">
                      Learn how to create custom nodes with our comprehensive SDK documentation.
                    </p>
                    <a
                      href="https://docs.loco.dev/sdk"
                      target="_blank"
                      rel="noopener noreferrer"
                      className="inline-flex items-center gap-2 text-sm text-blue-600 hover:text-blue-700 font-medium"
                    >
                      View Documentation
                      <ExternalLink className="w-4 h-4" />
                    </a>
                  </div>

                  <div className="p-6 bg-green-50 border border-green-200 rounded-lg">
                    <Package className="w-8 h-8 text-green-600 mb-3" />
                    <h4 className="text-lg font-semibold text-gray-900 mb-2">Starter Template</h4>
                    <p className="text-sm text-gray-700 mb-4">
                      Get started quickly with our plugin starter template and example nodes.
                    </p>
                    <a
                      href="https://github.com/loco/plugin-template"
                      target="_blank"
                      rel="noopener noreferrer"
                      className="inline-flex items-center gap-2 text-sm text-green-600 hover:text-green-700 font-medium"
                    >
                      Clone Template
                      <ExternalLink className="w-4 h-4" />
                    </a>
                  </div>
                </div>

                <div className="p-6 bg-white border border-gray-200 rounded-lg">
                  <h4 className="text-lg font-semibold text-gray-900 mb-4">Quick Start Guide</h4>
                  <ol className="space-y-3 text-sm text-gray-700">
                    <li className="flex items-start gap-3">
                      <span className="flex-shrink-0 w-6 h-6 bg-loco-primary text-white rounded-full flex items-center justify-center text-xs font-bold">
                        1
                      </span>
                      <span>Clone the plugin template from GitHub</span>
                    </li>
                    <li className="flex items-start gap-3">
                      <span className="flex-shrink-0 w-6 h-6 bg-loco-primary text-white rounded-full flex items-center justify-center text-xs font-bold">
                        2
                      </span>
                      <span>Define your node types in <code className="px-1 py-0.5 bg-gray-100 rounded text-xs">nodes.json</code></span>
                    </li>
                    <li className="flex items-start gap-3">
                      <span className="flex-shrink-0 w-6 h-6 bg-loco-primary text-white rounded-full flex items-center justify-center text-xs font-bold">
                        3
                      </span>
                      <span>Implement node logic in TypeScript/JavaScript</span>
                    </li>
                    <li className="flex items-start gap-3">
                      <span className="flex-shrink-0 w-6 h-6 bg-loco-primary text-white rounded-full flex items-center justify-center text-xs font-bold">
                        4
                      </span>
                      <span>Test your plugin locally with <code className="px-1 py-0.5 bg-gray-100 rounded text-xs">npm run dev</code></span>
                    </li>
                    <li className="flex items-start gap-3">
                      <span className="flex-shrink-0 w-6 h-6 bg-loco-primary text-white rounded-full flex items-center justify-center text-xs font-bold">
                        5
                      </span>
                      <span>Publish to npm or load locally for testing</span>
                    </li>
                  </ol>
                </div>

                <div className="p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
                  <div className="flex items-start gap-3">
                    <AlertCircle className="w-5 h-5 text-yellow-600 flex-shrink-0 mt-0.5" />
                    <div className="text-sm text-yellow-700">
                      <p className="font-semibold mb-1">Plugin Development Tips</p>
                      <ul className="list-disc list-inside space-y-1">
                        <li>Follow TypeScript best practices for type safety</li>
                        <li>Include comprehensive error handling</li>
                        <li>Write unit tests for your nodes</li>
                        <li>Document all node inputs and outputs</li>
                        <li>Add example workflows to help users</li>
                      </ul>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          ) : (
            // Marketplace and Installed Tabs
            <>
              {/* Search and Filters */}
              <div className="p-6 border-b border-gray-200">
                <div className="flex gap-4">
                  <div className="flex-1 relative">
                    <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                    <input
                      type="text"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder="Search plugins..."
                      className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent"
                    />
                  </div>
                  <div className="relative">
                    <Filter className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                    <select
                      value={selectedCategory}
                      onChange={(e) => setSelectedCategory(e.target.value as PluginCategory)}
                      className="pl-10 pr-8 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-loco-primary focus:border-transparent appearance-none bg-white"
                    >
                      <option value="all">All Categories</option>
                      <option value="data">Data</option>
                      <option value="integration">Integration</option>
                      <option value="transformation">Transformation</option>
                      <option value="utility">Utility</option>
                      <option value="ai">AI/ML</option>
                    </select>
                  </div>
                </div>
              </div>

              {/* Plugins List */}
              <div className="p-6">
                {isLoading ? (
                  <div className="flex items-center justify-center py-12">
                    <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-loco-primary"></div>
                  </div>
                ) : filteredPlugins.length === 0 ? (
                  <div className="text-center py-12">
                    <Package className="w-16 h-16 text-gray-300 mx-auto mb-4" />
                    <p className="text-gray-500">No plugins found</p>
                  </div>
                ) : (
                  <div className="grid grid-cols-1 gap-4">
                    {filteredPlugins.map((plugin) => (
                      <div
                        key={plugin.id}
                        className="p-6 bg-white border border-gray-200 rounded-lg hover:shadow-md transition-shadow"
                      >
                        <div className="flex items-start justify-between mb-4">
                          <div className="flex-1">
                            <div className="flex items-center gap-3 mb-2">
                              <h3 className="text-lg font-semibold text-gray-900">{plugin.name}</h3>
                              <span className={`px-2 py-1 text-xs font-medium rounded ${getCategoryColor(plugin.category)}`}>
                                {plugin.category}
                              </span>
                              {plugin.status === 'installed' && (
                                <span className="flex items-center gap-1 px-2 py-1 text-xs font-medium bg-green-100 text-green-700 rounded">
                                  <CheckCircle className="w-3 h-3" />
                                  Installed
                                </span>
                              )}
                            </div>
                            <p className="text-sm text-gray-600 mb-2">{plugin.description}</p>
                            <div className="flex items-center gap-4 text-xs text-gray-500">
                              <span>v{plugin.version}</span>
                              <span>by {plugin.author}</span>
                              <span className="flex items-center gap-1">
                                <Download className="w-3 h-3" />
                                {plugin.downloads.toLocaleString()} downloads
                              </span>
                              <span className="flex items-center gap-1">
                                <Star className="w-3 h-3 fill-yellow-400 text-yellow-400" />
                                {plugin.rating}
                              </span>
                            </div>
                          </div>
                          <div className="flex items-center gap-2">
                            {plugin.status === 'installed' ? (
                              <>
                                <button
                                  onClick={() => handleTogglePlugin(plugin)}
                                  className={`p-2 rounded-lg transition-colors ${
                                    plugin.enabled
                                      ? 'bg-green-100 text-green-700 hover:bg-green-200'
                                      : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                                  }`}
                                  title={plugin.enabled ? 'Disable' : 'Enable'}
                                >
                                  {plugin.enabled ? <Power className="w-4 h-4" /> : <PowerOff className="w-4 h-4" />}
                                </button>
                                <button
                                  onClick={() => handleUninstallPlugin(plugin)}
                                  className="p-2 bg-red-100 text-red-700 hover:bg-red-200 rounded-lg transition-colors"
                                  title="Uninstall"
                                >
                                  <Trash2 className="w-4 h-4" />
                                </button>
                              </>
                            ) : (
                              <button
                                onClick={() => handleInstallPlugin(plugin)}
                                className="flex items-center gap-2 px-4 py-2 bg-loco-primary text-white rounded-lg hover:bg-blue-700 transition-colors"
                              >
                                <Download className="w-4 h-4" />
                                Install
                              </button>
                            )}
                          </div>
                        </div>

                        {/* Nodes */}
                        <div className="mb-4">
                          <h4 className="text-sm font-semibold text-gray-700 mb-2">Included Nodes:</h4>
                          <div className="flex flex-wrap gap-2">
                            {plugin.nodes.map((node) => (
                              <div
                                key={node.id}
                                className="px-3 py-1.5 bg-gray-50 border border-gray-200 rounded text-xs"
                                title={node.description}
                              >
                                {node.name}
                              </div>
                            ))}
                          </div>
                        </div>

                        {/* Links */}
                        <div className="flex items-center gap-4">
                          {plugin.documentation && (
                            <a
                              href={plugin.documentation}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="flex items-center gap-1 text-xs text-blue-600 hover:text-blue-700"
                            >
                              <Book className="w-3 h-3" />
                              Documentation
                            </a>
                          )}
                          {plugin.repository && (
                            <a
                              href={plugin.repository}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="flex items-center gap-1 text-xs text-gray-600 hover:text-gray-700"
                            >
                              <Code className="w-3 h-3" />
                              Repository
                            </a>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-gray-200 bg-gray-50">
          <div className="flex items-center justify-between text-sm">
            <div className="text-gray-600">
              {activeTab === 'marketplace' && `${filteredPlugins.length} plugin${filteredPlugins.length !== 1 ? 's' : ''} available`}
              {activeTab === 'installed' && `${filteredPlugins.length} plugin${filteredPlugins.length !== 1 ? 's' : ''} installed`}
            </div>
            <button
              onClick={onClose}
              className="px-4 py-2 bg-white border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition-colors"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
