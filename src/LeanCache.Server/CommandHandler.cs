using System.Globalization;
using System.Text;
using LeanCache.Core;
using LeanCache.Protocol;

namespace LeanCache.Server;

/// <summary>
/// Routes RESP command arrays to the cache engine and returns RESP responses.
/// </summary>
public sealed class CommandHandler
{
    private readonly CacheStore _store;
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    public CommandHandler(CacheStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Executes a RESP command array and returns the response.
    /// </summary>
    public RespValue Execute(RespValue request)
    {
        if (request.Type != RespType.Array || request.ArrayValue is null || request.ArrayValue.Length == 0)
        {
            return RespValue.Error("ERR invalid command format");
        }

        var args = request.ArrayValue;
        var command = GetString(args[0]).ToUpperInvariant();

        return command switch
        {
            "PING" => HandlePing(args),
            "ECHO" => HandleEcho(args),
            "SET" => HandleSet(args),
            "GET" => HandleGet(args),
            "DEL" => HandleDel(args),
            "EXISTS" => HandleExists(args),
            "EXPIRE" => HandleExpire(args),
            "TTL" => HandleTtl(args),
            "PERSIST" => HandlePersist(args),
            "KEYS" => HandleKeys(args),
            "DBSIZE" => HandleDbSize(),
            "FLUSHDB" or "FLUSHALL" => HandleFlush(),
            "INFO" => HandleInfo(args),
            "COMMAND" => HandleCommand(),
            "QUIT" => RespValue.Ok,
            _ => RespValue.Error($"ERR unknown command '{command}'"),
        };
    }

    // PING [message]
    private static RespValue HandlePing(RespValue[] args)
    {
        if (args.Length > 1)
        {
            return RespValue.BulkString(GetString(args[1]));
        }

        return RespValue.Pong;
    }

    // ECHO message
    private static RespValue HandleEcho(RespValue[] args)
    {
        if (args.Length < 2)
        {
            return RespValue.Error("ERR wrong number of arguments for 'echo' command");
        }

        return RespValue.BulkString(GetString(args[1]));
    }

    // SET key value [EX seconds | PX milliseconds]
    private RespValue HandleSet(RespValue[] args)
    {
        if (args.Length < 3)
        {
            return RespValue.Error("ERR wrong number of arguments for 'set' command");
        }

        var key = GetString(args[1]);
        var value = Encoding.UTF8.GetBytes(GetString(args[2]));
        TimeSpan? ttl = null;

        // Parse optional EX/PX
        for (var i = 3; i < args.Length - 1; i++)
        {
            var option = GetString(args[i]).ToUpperInvariant();
            var optionValue = GetString(args[i + 1]);

            if (option == "EX" && long.TryParse(optionValue, out var seconds))
            {
                ttl = TimeSpan.FromSeconds(seconds);
                i++;
            }
            else if (option == "PX" && long.TryParse(optionValue, out var milliseconds))
            {
                ttl = TimeSpan.FromMilliseconds(milliseconds);
                i++;
            }
        }

        _store.Set(key, value, ttl);
        return RespValue.Ok;
    }

    // GET key
    private RespValue HandleGet(RespValue[] args)
    {
        if (args.Length < 2)
        {
            return RespValue.Error("ERR wrong number of arguments for 'get' command");
        }

        var value = _store.Get(GetString(args[1]));

        if (value is null)
        {
            return RespValue.Null;
        }

        return RespValue.BulkString(Encoding.UTF8.GetString(value));
    }

    // DEL key [key ...]
    private RespValue HandleDel(RespValue[] args)
    {
        if (args.Length < 2)
        {
            return RespValue.Error("ERR wrong number of arguments for 'del' command");
        }

        var deleted = 0;

        for (var i = 1; i < args.Length; i++)
        {
            if (_store.Delete(GetString(args[i])))
            {
                deleted++;
            }
        }

        return RespValue.IntegerFrom(deleted);
    }

    // EXISTS key [key ...]
    private RespValue HandleExists(RespValue[] args)
    {
        if (args.Length < 2)
        {
            return RespValue.Error("ERR wrong number of arguments for 'exists' command");
        }

        var count = 0;

        for (var i = 1; i < args.Length; i++)
        {
            if (_store.Exists(GetString(args[i])))
            {
                count++;
            }
        }

        return RespValue.IntegerFrom(count);
    }

    // EXPIRE key seconds
    private RespValue HandleExpire(RespValue[] args)
    {
        if (args.Length < 3)
        {
            return RespValue.Error("ERR wrong number of arguments for 'expire' command");
        }

        if (!long.TryParse(GetString(args[2]), out var seconds))
        {
            return RespValue.Error("ERR value is not an integer or out of range");
        }

        var result = _store.Expire(GetString(args[1]), TimeSpan.FromSeconds(seconds));
        return result ? RespValue.One : RespValue.Zero;
    }

    // TTL key → returns -2 (missing), -1 (no expiry), or seconds remaining
    private RespValue HandleTtl(RespValue[] args)
    {
        if (args.Length < 2)
        {
            return RespValue.Error("ERR wrong number of arguments for 'ttl' command");
        }

        var key = GetString(args[1]);

        if (!_store.Exists(key))
        {
            return RespValue.IntegerFrom(-2); // Key does not exist
        }

        var ttl = _store.GetTimeToLive(key);

        if (ttl is null)
        {
            return RespValue.IntegerFrom(-1); // No expiry
        }

        var seconds = (long)Math.Ceiling(ttl.Value.TotalSeconds);
        return RespValue.IntegerFrom(seconds > 0 ? seconds : -2);
    }

    // PERSIST key
    private RespValue HandlePersist(RespValue[] args)
    {
        if (args.Length < 2)
        {
            return RespValue.Error("ERR wrong number of arguments for 'persist' command");
        }

        var result = _store.Persist(GetString(args[1]));
        return result ? RespValue.One : RespValue.Zero;
    }

    // KEYS pattern
    private RespValue HandleKeys(RespValue[] args)
    {
        var pattern = args.Length > 1 ? GetString(args[1]) : "*";
        var keys = _store.Keys(pattern);
        var elements = new RespValue[keys.Count];

        for (var i = 0; i < keys.Count; i++)
        {
            elements[i] = RespValue.BulkString(keys[i]);
        }

        return RespValue.Array(elements);
    }

    // DBSIZE
    private RespValue HandleDbSize()
    {
        return RespValue.IntegerFrom(_store.Count);
    }

    // FLUSHDB / FLUSHALL
    private RespValue HandleFlush()
    {
        _store.Clear();
        return RespValue.Ok;
    }

    // INFO [section]
    private RespValue HandleInfo(RespValue[] args)
    {
        var section = args.Length > 1 ? GetString(args[1]).ToUpperInvariant() : "ALL";
        var sb = new StringBuilder();

        if (section is "ALL" or "SERVER")
        {
            var uptime = DateTimeOffset.UtcNow - _startTime;
            sb.AppendLine("# Server");
            sb.AppendLine("lean_cache_version:1.0.0");
            sb.AppendLine(CultureInfo.InvariantCulture, $"uptime_in_seconds:{(long)uptime.TotalSeconds}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"uptime_in_days:{(long)uptime.TotalDays}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"tcp_port:{Environment.GetEnvironmentVariable("LEANCACHE_PORT") ?? "6379"}");
            sb.AppendLine();
        }

        if (section is "ALL" or "KEYSPACE")
        {
            var stats = _store.GetStats();
            sb.AppendLine("# Keyspace");
            sb.AppendLine(CultureInfo.InvariantCulture, $"db0:keys={stats.KeyCount},expired={stats.ExpiredKeysRemoved}");
            sb.AppendLine();
        }

        return RespValue.BulkString(sb.ToString().TrimEnd());
    }

    // COMMAND — redis-cli sends this on connect
    private static RespValue HandleCommand()
    {
        return RespValue.EmptyArray;
    }

    private static string GetString(RespValue value)
    {
        return value.StringValue ?? string.Empty;
    }
}
