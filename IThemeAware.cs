namespace Toolbox
{
    /// <summary>
    /// 由工具 UserControl 实现，可在主题切换时由 MainWindow 通知刷新自身主题相关资源
    /// （如标题栏 LogoImage 的白/黑图标）。未实现则不刷新。
    /// </summary>
    public interface IThemeAware
    {
        /// <summary>当前主题变更后调用，isDark=true 表示深色主题。</summary>
        void RefreshTheme(bool isDark);
    }
}
