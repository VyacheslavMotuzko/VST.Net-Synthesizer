using System;
using System.Windows.Controls;

namespace SynthNet.UI
{
    public partial class View 
    {
        public View()
        {
            InitializeComponent();
		}

        public event Action<TextBlock> LFOParameterChanged;
        public event Action<TextBlock> LFOBParameterChanged;

        private void LFOParamsListOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LFOParameterChanged?.Invoke(e.AddedItems[0] as TextBlock);
        }

        private void LFOParamsListBonSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LFOBParameterChanged?.Invoke(e.AddedItems[0] as TextBlock);
        }
    }
}
