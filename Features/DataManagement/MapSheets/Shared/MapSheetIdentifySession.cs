namespace XIAOFUTools.Shared
{
    internal sealed class MapSheetIdentifySession
    {
        public bool IsActive { get; private set; }

        public bool CanStart => !IsActive;

        public bool CanStop => IsActive;

        public string StatusText => IsActive
            ? "已启动，当前可连续查询，点击“退出查询”可取消。"
            : "未启动";

        public void Start()
        {
            IsActive = true;
        }

        public void Stop()
        {
            IsActive = false;
        }
    }
}
