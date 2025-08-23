using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;

namespace Loco.Core.Blockchain;

/// <summary>
/// Immutable audit trail using blockchain technology
/// Implements a lightweight blockchain for audit logging without external dependencies
/// </summary>
public sealed class BlockchainAuditService
{
    private readonly ILogger<BlockchainAuditService> _logger;
    private readonly List<Block> _chain;
    private readonly List<Transaction> _pendingTransactions;
    private readonly object _chainLock = new();
    private readonly Timer _miningTimer;
    private readonly string _persistencePath;
    
    // Blockchain parameters
    private const int Difficulty = 4; // Number of leading zeros required in hash
    private const int BlockSize = 10; // Transactions per block
    private const int MiningReward = 1;
    private readonly string _difficultyPrefix;
    
    public class Block
    {
        public int Index { get; set; }
        public DateTime Timestamp { get; set; }
        public List<Transaction> Transactions { get; set; }
        public string PreviousHash { get; set; }
        public string Hash { get; set; }
        public int Nonce { get; set; }
        public string MerkleRoot { get; set; }
        
        public Block(int index, List<Transaction> transactions, string previousHash)
        {
            Index = index;
            Timestamp = DateTime.UtcNow;
            Transactions = transactions;
            PreviousHash = previousHash;
            Nonce = 0;
            MerkleRoot = CalculateMerkleRoot(transactions);
        }
        
        public string CalculateHash()
        {
            using var sha256 = SHA256.Create();
            var data = $"{Index}{Timestamp:O}{MerkleRoot}{PreviousHash}{Nonce}";
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(bytes);
        }
        
        private static string CalculateMerkleRoot(List<Transaction> transactions)
        {
            if (transactions == null || transactions.Count == 0)
                return string.Empty;
            
            var hashes = transactions.Select(t => t.GetHash()).ToList();
            
            while (hashes.Count > 1)
            {
                var newHashes = new List<string>();
                
                for (int i = 0; i < hashes.Count; i += 2)
                {
                    var hash1 = hashes[i];
                    var hash2 = (i + 1 < hashes.Count) ? hashes[i + 1] : hashes[i];
                    
                    using var sha256 = SHA256.Create();
                    var combined = hash1 + hash2;
                    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                    newHashes.Add(Convert.ToBase64String(bytes));
                }
                
                hashes = newHashes;
            }
            
            return hashes.FirstOrDefault() ?? string.Empty;
        }
        
        public void MineBlock(int difficulty)
        {
            var prefix = new string('0', difficulty);
            
            while (!Hash?.StartsWith(prefix) ?? true)
            {
                Nonce++;
                Hash = CalculateHash();
            }
        }
    }
    
    public class Transaction
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; }
        public string Actor { get; set; }
        public string Action { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public string Signature { get; set; }
        
        public Transaction(string type, string actor, string action, Dictionary<string, object> data = null)
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
            Type = type;
            Actor = actor;
            Action = action;
            Data = data ?? new Dictionary<string, object>();
        }
        
        public string GetHash()
        {
            using var sha256 = SHA256.Create();
            var json = JsonSerializer.Serialize(new { Id, Timestamp, Type, Actor, Action, Data });
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
            return Convert.ToBase64String(bytes);
        }
        
        public void Sign(string privateKey)
        {
            // Simplified signature - in production use proper RSA/ECDSA
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(privateKey));
            var hash = GetHash();
            var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(hash));
            Signature = Convert.ToBase64String(signature);
        }
        
        public bool VerifySignature(string publicKey)
        {
            if (string.IsNullOrEmpty(Signature))
                return false;
            
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(publicKey));
            var hash = GetHash();
            var computedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(hash));
            var computedBase64 = Convert.ToBase64String(computedSignature);
            
            return computedBase64 == Signature;
        }
    }
    
    public BlockchainAuditService(ILogger<BlockchainAuditService> logger, string persistencePath = null)
    {
        _logger = logger;
        _chain = new List<Block>();
        _pendingTransactions = new List<Transaction>();
        _persistencePath = persistencePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Loco",
            "blockchain.json");
        
        _difficultyPrefix = new string('0', Difficulty);
        
        // Load existing chain or create genesis block
        if (!LoadChain())
        {
            CreateGenesisBlock();
        }
        
        // Start mining timer
        _miningTimer = new Timer(MineBlocks, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        _logger.LogInformation("Blockchain audit service initialized with {Blocks} blocks", _chain.Count);
    }
    
    /// <summary>
    /// Add an audit entry to the blockchain
    /// </summary>
    public async Task<string> AddAuditEntryAsync(
        string actor,
        string action,
        string entityType,
        Dictionary<string, object> data = null,
        string privateKey = null)
    {
        var transaction = new Transaction(entityType, actor, action, data);
        
        if (!string.IsNullOrEmpty(privateKey))
        {
            transaction.Sign(privateKey);
        }
        
        lock (_chainLock)
        {
            _pendingTransactions.Add(transaction);
        }
        
        _logger.LogDebug("Added audit transaction {Id} to pending pool", transaction.Id);
        
        // Mine immediately if we have enough transactions
        if (_pendingTransactions.Count >= BlockSize)
        {
            await Task.Run(() => MineBlocks(null));
        }
        
        return transaction.Id;
    }
    
    /// <summary>
    /// Verify the integrity of the entire blockchain
    /// </summary>
    public BlockchainIntegrity VerifyChainIntegrity()
    {
        lock (_chainLock)
        {
            var result = new BlockchainIntegrity
            {
                IsValid = true,
                TotalBlocks = _chain.Count,
                VerifiedBlocks = 0
            };
            
            for (int i = 1; i < _chain.Count; i++)
            {
                var currentBlock = _chain[i];
                var previousBlock = _chain[i - 1];
                
                // Verify current block hash
                if (currentBlock.Hash != currentBlock.CalculateHash())
                {
                    result.IsValid = false;
                    result.InvalidBlocks.Add(new InvalidBlock
                    {
                        Index = currentBlock.Index,
                        Reason = "Hash mismatch"
                    });
                    continue;
                }
                
                // Verify link to previous block
                if (currentBlock.PreviousHash != previousBlock.Hash)
                {
                    result.IsValid = false;
                    result.InvalidBlocks.Add(new InvalidBlock
                    {
                        Index = currentBlock.Index,
                        Reason = "Previous hash mismatch"
                    });
                    continue;
                }
                
                // Verify proof of work
                if (!currentBlock.Hash.StartsWith(_difficultyPrefix))
                {
                    result.IsValid = false;
                    result.InvalidBlocks.Add(new InvalidBlock
                    {
                        Index = currentBlock.Index,
                        Reason = "Invalid proof of work"
                    });
                    continue;
                }
                
                // Verify Merkle root
                var calculatedMerkleRoot = Block.CalculateMerkleRoot(currentBlock.Transactions);
                if (currentBlock.MerkleRoot != calculatedMerkleRoot)
                {
                    result.IsValid = false;
                    result.InvalidBlocks.Add(new InvalidBlock
                    {
                        Index = currentBlock.Index,
                        Reason = "Merkle root mismatch"
                    });
                    continue;
                }
                
                result.VerifiedBlocks++;
            }
            
            return result;
        }
    }
    
    /// <summary>
    /// Query audit entries
    /// </summary>
    public List<Transaction> QueryAuditLog(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string actor = null,
        string action = null,
        string entityType = null)
    {
        lock (_chainLock)
        {
            var transactions = _chain.SelectMany(b => b.Transactions);
            
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Timestamp >= startDate.Value);
            
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Timestamp <= endDate.Value);
            
            if (!string.IsNullOrEmpty(actor))
                transactions = transactions.Where(t => t.Actor == actor);
            
            if (!string.IsNullOrEmpty(action))
                transactions = transactions.Where(t => t.Action == action);
            
            if (!string.IsNullOrEmpty(entityType))
                transactions = transactions.Where(t => t.Type == entityType);
            
            return transactions.ToList();
        }
    }
    
    /// <summary>
    /// Get specific transaction by ID
    /// </summary>
    public Transaction GetTransaction(string transactionId)
    {
        lock (_chainLock)
        {
            return _chain
                .SelectMany(b => b.Transactions)
                .FirstOrDefault(t => t.Id == transactionId);
        }
    }
    
    /// <summary>
    /// Get block by index
    /// </summary>
    public Block GetBlock(int index)
    {
        lock (_chainLock)
        {
            return _chain.FirstOrDefault(b => b.Index == index);
        }
    }
    
    /// <summary>
    /// Get blockchain statistics
    /// </summary>
    public BlockchainStatistics GetStatistics()
    {
        lock (_chainLock)
        {
            var allTransactions = _chain.SelectMany(b => b.Transactions).ToList();
            
            return new BlockchainStatistics
            {
                TotalBlocks = _chain.Count,
                TotalTransactions = allTransactions.Count,
                PendingTransactions = _pendingTransactions.Count,
                ChainSizeBytes = CalculateChainSize(),
                AverageBlockTime = CalculateAverageBlockTime(),
                LastBlockTime = _chain.LastOrDefault()?.Timestamp,
                Difficulty = Difficulty,
                TransactionTypes = allTransactions
                    .GroupBy(t => t.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                TopActors = allTransactions
                    .GroupBy(t => t.Actor)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
    }
    
    /// <summary>
    /// Export blockchain to JSON
    /// </summary>
    public string ExportChain()
    {
        lock (_chainLock)
        {
            return JsonSerializer.Serialize(_chain, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }
    
    /// <summary>
    /// Import and validate a blockchain
    /// </summary>
    public bool ImportChain(string json)
    {
        try
        {
            var newChain = JsonSerializer.Deserialize<List<Block>>(json);
            
            if (newChain == null || newChain.Count == 0)
                return false;
            
            // Validate the imported chain
            var tempService = new BlockchainAuditService(_logger, null);
            tempService._chain.Clear();
            tempService._chain.AddRange(newChain);
            
            var integrity = tempService.VerifyChainIntegrity();
            if (!integrity.IsValid)
                return false;
            
            // Replace current chain if valid
            lock (_chainLock)
            {
                _chain.Clear();
                _chain.AddRange(newChain);
                SaveChain();
            }
            
            _logger.LogInformation("Successfully imported blockchain with {Blocks} blocks", newChain.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import blockchain");
            return false;
        }
    }
    
    private void CreateGenesisBlock()
    {
        var genesisTransactions = new List<Transaction>
        {
            new Transaction("SYSTEM", "GENESIS", "CREATE", new Dictionary<string, object>
            {
                ["message"] = "Loco Blockchain Genesis Block",
                ["timestamp"] = DateTime.UtcNow,
                ["version"] = "1.0.0"
            })
        };
        
        var genesisBlock = new Block(0, genesisTransactions, "0");
        genesisBlock.MineBlock(Difficulty);
        
        lock (_chainLock)
        {
            _chain.Add(genesisBlock);
            SaveChain();
        }
        
        _logger.LogInformation("Genesis block created with hash: {Hash}", genesisBlock.Hash);
    }
    
    private void MineBlocks(object state)
    {
        lock (_chainLock)
        {
            if (_pendingTransactions.Count == 0)
                return;
            
            while (_pendingTransactions.Count > 0)
            {
                var transactions = _pendingTransactions
                    .Take(BlockSize)
                    .ToList();
                
                _pendingTransactions.RemoveRange(0, transactions.Count);
                
                var previousBlock = _chain.Last();
                var newBlock = new Block(_chain.Count, transactions, previousBlock.Hash);
                
                _logger.LogInformation("Mining block {Index} with {Transactions} transactions...", 
                    newBlock.Index, transactions.Count);
                
                var startTime = DateTime.UtcNow;
                newBlock.MineBlock(Difficulty);
                var miningTime = DateTime.UtcNow - startTime;
                
                _chain.Add(newBlock);
                SaveChain();
                
                _logger.LogInformation("Block {Index} mined in {Time}ms. Hash: {Hash}", 
                    newBlock.Index, miningTime.TotalMilliseconds, newBlock.Hash);
            }
        }
    }
    
    private bool LoadChain()
    {
        try
        {
            if (!File.Exists(_persistencePath))
                return false;
            
            var json = File.ReadAllText(_persistencePath);
            var chain = JsonSerializer.Deserialize<List<Block>>(json);
            
            if (chain != null && chain.Count > 0)
            {
                lock (_chainLock)
                {
                    _chain.Clear();
                    _chain.AddRange(chain);
                }
                
                _logger.LogInformation("Loaded blockchain with {Blocks} blocks", chain.Count);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blockchain");
        }
        
        return false;
    }
    
    private void SaveChain()
    {
        try
        {
            var directory = Path.GetDirectoryName(_persistencePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            var json = JsonSerializer.Serialize(_chain, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(_persistencePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save blockchain");
        }
    }
    
    private long CalculateChainSize()
    {
        var json = JsonSerializer.Serialize(_chain);
        return Encoding.UTF8.GetByteCount(json);
    }
    
    private TimeSpan CalculateAverageBlockTime()
    {
        if (_chain.Count < 2)
            return TimeSpan.Zero;
        
        var totalTime = TimeSpan.Zero;
        for (int i = 1; i < _chain.Count; i++)
        {
            totalTime += _chain[i].Timestamp - _chain[i - 1].Timestamp;
        }
        
        return TimeSpan.FromMilliseconds(totalTime.TotalMilliseconds / (_chain.Count - 1));
    }
}

// Supporting classes
public class BlockchainIntegrity
{
    public bool IsValid { get; set; }
    public int TotalBlocks { get; set; }
    public int VerifiedBlocks { get; set; }
    public List<InvalidBlock> InvalidBlocks { get; set; } = new();
}

public class InvalidBlock
{
    public int Index { get; set; }
    public string Reason { get; set; }
}

public class BlockchainStatistics
{
    public int TotalBlocks { get; set; }
    public int TotalTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public long ChainSizeBytes { get; set; }
    public TimeSpan AverageBlockTime { get; set; }
    public DateTime? LastBlockTime { get; set; }
    public int Difficulty { get; set; }
    public Dictionary<string, int> TransactionTypes { get; set; }
    public Dictionary<string, int> TopActors { get; set; }
}
