using System;

/// <summary>
/// 标识一次仍受 AudioManager 管理的播放，用于停止循环音效或提前中断播放。
/// 默认值表示无效句柄。
/// </summary>
public readonly struct AudioPlaybackHandle : IEquatable<AudioPlaybackHandle>
{
    readonly int id;

    internal AudioPlaybackHandle(int id)
    {
        this.id = id;
    }

    internal int Id => id;
    public bool IsValid => id != 0;

    public bool Equals(AudioPlaybackHandle other)
    {
        return id == other.id;
    }

    public override bool Equals(object obj)
    {
        return obj is AudioPlaybackHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        return id;
    }

    public static bool operator ==(AudioPlaybackHandle left, AudioPlaybackHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(AudioPlaybackHandle left, AudioPlaybackHandle right)
    {
        return !left.Equals(right);
    }
}
