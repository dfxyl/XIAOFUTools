namespace XIAOFUTools.Shared.Diagnostics
{
    internal interface IOperationProgress
    {
        void Report(int percentage, string status = null);
    }
}
