using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WinMeter.Metrics;

/// <summary>
/// Top processes from the x64 SYSTEM_PROCESS_INFORMATION layout. No admin rights.
/// </summary>
internal sealed class ProcessList : IDisposable
{
    private const int SystemProcessInformation = 5;
    private const uint StatusInfoLengthMismatch = 0xC0000004;
    private readonly Dictionary<int, long> _previous = new();
    private IntPtr _buffer;
    private int _length;
    private bool _hasPrevious;

    public (ProcessLoad[] Cpu, ProcessLoad[] Memory) Sample(double elapsedSeconds, int processors)
    {
        var rows = Read();
        if (rows == null)
        {
            return (Array.Empty<ProcessLoad>(), Array.Empty<ProcessLoad>());
        }

        var cpu = new List<Ranked>();
        var memory = new List<Ranked>();
        foreach (var row in rows)
        {
            if (row.Pid == 0 || row.Name.Length == 0)
            {
                continue;
            }

            if (row.Memory > 0)
            {
                memory.Add(new Ranked(row.Pid, row.Name, 0, row.Memory));
            }

            if (_hasPrevious && elapsedSeconds > 0 && _previous.TryGetValue(row.Pid, out var before))
            {
                var delta = row.Time - before;
                if (delta < 0)
                {
                    continue;
                }

                var percent = delta / 10_000_000d / elapsedSeconds / Math.Max(1, processors) * 100;
                if (percent >= 0.1)
                {
                    cpu.Add(new Ranked(row.Pid, row.Name, Math.Clamp(percent, 0, 100), row.Memory));
                }
            }
        }

        _previous.Clear();
        foreach (var row in rows)
        {
            _previous[row.Pid] = row.Time;
        }

        _hasPrevious = true;
        cpu.Sort(static (left, right) => right.Cpu.CompareTo(left.Cpu));
        memory.Sort(static (left, right) => right.Memory.CompareTo(left.Memory));
        return (Take(cpu, 5, true), Take(memory, 5, false));
    }

    public void Dispose()
    {
        if (_buffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_buffer);
            _buffer = IntPtr.Zero;
        }
    }

    private Raw[]? Read()
    {
        if (IntPtr.Size != 8)
        {
            return null;
        }

        if (_buffer == IntPtr.Zero)
        {
            _length = 256 * 1024;
            _buffer = Marshal.AllocHGlobal(_length);
        }

        uint status = 0;
        var returned = 0;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            status = NtQuerySystemInformation(SystemProcessInformation, _buffer, _length, out returned);
            if (status == StatusInfoLengthMismatch && returned > 0 && returned < 16 * 1024 * 1024)
            {
                Marshal.FreeHGlobal(_buffer);
                _length = returned + 8192;
                _buffer = Marshal.AllocHGlobal(_length);
                continue;
            }

            break;
        }

        if (status != 0 || returned < 160)
        {
            return null;
        }

        var rows = new List<Raw>(128);
        var offset = 0;
        for (var n = 0; n < 4096 && offset + 160 <= returned; n++)
        {
            var next = Marshal.ReadInt32(_buffer, offset);
            var pid = (int)Marshal.ReadInt64(_buffer, offset + 80);
            var user = Marshal.ReadInt64(_buffer, offset + 40);
            var kernel = Marshal.ReadInt64(_buffer, offset + 48);
            var privateBytes = Marshal.ReadInt64(_buffer, offset + 8);
            var workingSet = Marshal.ReadInt64(_buffer, offset + 144);
            var memory = privateBytes > 0 ? privateBytes : workingSet;
            rows.Add(new Raw(pid, NameAt(offset), user + kernel, memory < 0 ? 0 : memory));
            if (next <= 0)
            {
                break;
            }

            if (next < 64 || offset + next > returned)
            {
                break;
            }

            offset += next;
        }

        if (rows.Count == 0 || rows[0].Pid != 0)
        {
            return null;
        }

        var named = 0;
        foreach (var row in rows)
        {
            if (row.Name.Length > 0)
            {
                named++;
            }
        }

        return named >= 4 ? rows.ToArray() : null;
    }

    private string NameAt(int offset)
    {
        var length = Marshal.ReadInt16(_buffer, offset + 56);
        var pointer = Marshal.ReadIntPtr(_buffer, offset + 64);
        if (pointer == IntPtr.Zero || length <= 1 || length > 512)
        {
            return "";
        }

        var text = Marshal.PtrToStringUni(pointer, length / 2) ?? "";
        var end = text.IndexOf('\0');
        if (end >= 0)
        {
            text = text.Substring(0, end);
        }

        text = text.Trim();
        if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Substring(0, text.Length - 4);
        }

        return text;
    }

    private static ProcessLoad[] Take(List<Ranked> ranked, int limit, bool cpu)
    {
        var count = Math.Min(limit, ranked.Count);
        var duplicate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count; i++)
        {
            if (!seen.Add(ranked[i].Name))
            {
                duplicate.Add(ranked[i].Name);
            }
        }

        var result = new ProcessLoad[count];
        for (var i = 0; i < count; i++)
        {
            var item = ranked[i];
            var name = duplicate.Contains(item.Name) ? item.Name + " (" + item.Pid + ")" : item.Name;
            result[i] = new ProcessLoad(name, cpu ? item.Cpu : 0, item.Memory);
        }

        return result;
    }

    [DllImport("ntdll.dll")]
    private static extern uint NtQuerySystemInformation(int systemInformationClass, IntPtr buffer, int length, out int returnLength);

    private readonly record struct Raw(int Pid, string Name, long Time, long Memory);

    private readonly record struct Ranked(int Pid, string Name, double Cpu, long Memory);
}
