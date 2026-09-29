using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using SextaFeira.Domain.Entities;
using SextaFeira.Domain.Interfaces;

namespace SextaFeira.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<ConversationSession> Sessions => Set<ConversationSession>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<MemoryVectorRecord> Memories => Set<MemoryVectorRecord>();
    public DbSet<EncryptedSecretRecord> Secrets => Set<EncryptedSecretRecord>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<MemoryVectorRecord>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Embedding).HasColumnType("vector(384)");
            b.HasIndex(x => x.Category);
            b.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ChatMessage>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.SessionId);
        });

        modelBuilder.Entity<EncryptedSecretRecord>(b =>
        {
            b.HasKey(x => x.KeyName);
        });
    }
}

public class MemoryVectorRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = "Fact";
    public Vector? Embedding { get; set; }
    public double ImportanceScore { get; set; } = 1.0;
    public int AccessCount { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public bool IsRecycled { get; set; } = false;
}

public class EncryptedSecretRecord
{
    public string KeyName { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public string IvBase64 { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PgVectorMemoryStore : IMemoryStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILLMProvider _embeddingProvider;

    // Em-memory fallback se PostgreSQL não estiver conectado no instante
    private static readonly List<MemoryVector> _inMemoryCache = new()
    {
        new MemoryVector { Category = "Perfil", Content = "O usuário é um Arquiteto de Software e Engenheiro de IA.", ImportanceScore = 1.0 },
        new MemoryVector { Category = "Preferência", Content = "O usuário prefere respostas em PT-BR objetivas e em alto nível técnico.", ImportanceScore = 1.0 },
        new MemoryVector { Category = "Ambiente", Content = "Sistema operacional principal Windows, ambiente de execução Docker ativo.", ImportanceScore = 0.9 },
        new MemoryVector { Category = "Localização", Content = "Localização base em Piracaia, São Paulo, Brasil.", ImportanceScore = 0.8 }
    };

    public PgVectorMemoryStore(AppDbContext dbContext, IEnumerable<ILLMProvider> llmProviders)
    {
        _dbContext = dbContext;
        _embeddingProvider = llmProviders.First();
    }

    public async Task StoreMemoryAsync(MemoryVector memory, CancellationToken cancellationToken = default)
    {
        if (memory.Embedding == null || memory.Embedding.Length == 0)
        {
            memory.Embedding = await _embeddingProvider.GenerateEmbeddingAsync(memory.Content, cancellationToken);
        }

        try
        {
            var record = new MemoryVectorRecord
            {
                Id = memory.Id,
                Content = memory.Content,
                Category = memory.Category,
                Embedding = memory.Embedding != null ? new Vector(memory.Embedding) : null,
                ImportanceScore = memory.ImportanceScore,
                CreatedAt = memory.CreatedAt,
                LastAccessedAt = DateTime.UtcNow,
                IsRecycled = false
            };
            _dbContext.Memories.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Salvar no cache in-memory de contingência
            lock (_inMemoryCache)
            {
                _inMemoryCache.Add(memory);
            }
        }
    }

    public async Task<List<MemoryVector>> SearchSimilarAsync(string query, int limit = 5, double minSimilarity = 0.6, CancellationToken cancellationToken = default)
    {
        var queryEmbedding = await _embeddingProvider.GenerateEmbeddingAsync(query, cancellationToken);
        var queryVector = new Vector(queryEmbedding);

        try
        {
            var records = await _dbContext.Memories
                .Where(m => !m.IsRecycled)
                .OrderBy(m => m.Embedding!.CosineDistance(queryVector))
                .Take(limit)
                .ToListAsync(cancellationToken);

            if (records.Count > 0)
            {
                return records.Select(r => new MemoryVector
                {
                    Id = r.Id,
                    Content = r.Content,
                    Category = r.Category,
                    ImportanceScore = r.ImportanceScore,
                    CreatedAt = r.CreatedAt,
                    LastAccessedAt = r.LastAccessedAt
                }).ToList();
            }
        }
        catch
        {
            // Fallback in-memory
        }

        lock (_inMemoryCache)
        {
            return _inMemoryCache.Take(limit).ToList();
        }
    }

    public async Task<int> RecycleContextGarbageCollectionAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var thresholdDate = DateTime.UtcNow - olderThan;
        try
        {
            var oldMemories = await _dbContext.Memories
                .Where(m => m.CreatedAt < thresholdDate && m.ImportanceScore < 0.5 && !m.IsRecycled)
                .ToListAsync(cancellationToken);

            foreach (var mem in oldMemories)
            {
                mem.IsRecycled = true;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return oldMemories.Count;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<List<MemoryVector>> GetAllActiveMemoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var records = await _dbContext.Memories.Where(m => !m.IsRecycled).ToListAsync(cancellationToken);
            if (records.Count > 0)
            {
                return records.Select(r => new MemoryVector
                {
                    Id = r.Id,
                    Content = r.Content,
                    Category = r.Category,
                    ImportanceScore = r.ImportanceScore,
                    CreatedAt = r.CreatedAt
                }).ToList();
            }
        }
        catch { }

        lock (_inMemoryCache)
        {
            return _inMemoryCache.ToList();
        }
    }
}

public class AesEncryptionVault
{
    private readonly byte[] _key;

    public AesEncryptionVault(string secretKey)
    {
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(secretKey));
    }

    public (string CipherText, string IvBase64) Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        var ivBase64 = Convert.ToBase64String(aes.IV);

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        var cipherText = Convert.ToBase64String(ms.ToArray());
        return (cipherText, ivBase64);
    }

    public string Decrypt(string cipherText, string ivBase64)
    {
        var iv = Convert.FromBase64String(ivBase64);
        var buffer = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(buffer);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
