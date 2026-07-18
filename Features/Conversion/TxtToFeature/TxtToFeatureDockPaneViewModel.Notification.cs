using System.Runtime.CompilerServices;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    public partial class TxtToFeatureDockPaneViewModel
    {
        protected bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
