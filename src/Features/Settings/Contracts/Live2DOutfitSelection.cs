namespace Atlas.Features.Settings.Contracts;

/// <summary>
///     看板娘服装组当前选中的选项；每个参数只属于一个组，组内选项互斥
/// </summary>
public record Live2DOutfitSelection(string GroupId, string OptionId);