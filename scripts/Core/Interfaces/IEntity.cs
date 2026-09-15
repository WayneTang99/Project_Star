namespace Project_Star.Core.Interfaces;

// 实体复制契约（全局层）：实现类须支持复制独立实例（含独立属性集），供模板池调用。
public interface IEntity
{
    // 复制独立实例（深拷贝属性集/战斗状态）
    object Clone();
}