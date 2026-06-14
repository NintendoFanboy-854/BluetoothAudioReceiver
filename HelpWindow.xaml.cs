using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using BluetoothAudioReceiver.Services;

namespace BluetoothAudioReceiver;

public partial class HelpWindow : Window
{
    private readonly LocalizationService _loc;
    
    public HelpWindow()
    {
        InitializeComponent();
        _loc = LocalizationService.Instance;
        ApplyLocalization();
    }
    
    private void ApplyLocalization()
    {
        Title = _loc.Get("Help");
        TitleBarText.Text = _loc.Get("Help");
        
        CloseButton.ToolTip = _loc.Get("Close");
        AutomationProperties.SetName(CloseButton, _loc.Get("Close"));
        
        QuickStartHeader.Text = _loc.Get("QuickStart");
        QuickStartText.Text = _loc.Get("QuickStartSteps");
        
        KnownIssuesHeader.Text = _loc.Get("KnownIssues");
        
        NetworkSlowdownTitle.Text = _loc.Get("NetworkSlowdown");
        NetworkSlowdownDescText.Text = _loc.Get("NetworkSlowdownDesc");
        NetworkSolutionText.Text = $"{_loc.Get("Solution")}\n{_loc.Get("NetworkSolution")}";
        
        NoDevicesIssueTitle.Text = _loc.Get("NoDevicesIssue");
        NoDevicesSolutionText.Text = $"{_loc.Get("Solution")}\n{_loc.Get("NoDevicesSolution")}";
        
        ConnectionDropsTitle.Text = _loc.Get("ConnectionDrops");
        ConnectionDropsSolutionText.Text = $"{_loc.Get("Solution")}\n{_loc.Get("ConnectionDropsSolution")}";
        
        SystemRequirementsHeader.Text = _loc.Get("SystemRequirements");
        RequirementsText.Text = _loc.Get("Requirements");
        
        InfoHeader.Text = _loc.Get("Info");
        VersionLabelRun.Text = _loc.Get("Version");
        DevelopedWithRun.Text = _loc.Get("DevelopedWith");
    }
    
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
