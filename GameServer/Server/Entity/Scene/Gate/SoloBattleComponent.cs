using Fantasy.Entitas;
// ReSharper disable InconsistentNaming
#pragma warning disable CS8618

namespace Fantasy;

/// <summary>
/// 单人战斗的服务端校验状态，挂在 Session 上：断线即销毁，战斗作废。
/// 校验逻辑在共享的 GameBattle.BattleVerifier 里，联机房间以后复用同一个类。
/// </summary>
public sealed class SoloBattleComponent : Entity
{
    /// <summary>本局战斗 ID，客户端每次请求都要带上。</summary>
    public long BattleId;

    public GameBattle.BattleVerifier Verifier;

    /// <summary>开局的服务端时间（毫秒），用于检测加速。</summary>
    public long StartTimeMs;
}
