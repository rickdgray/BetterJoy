using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace EvenBetterJoy.Models
{
    public class OutputControllerDualShock4
    {
        private readonly IDualShock4Controller controller;

        private OutputControllerDualShock4InputState currentState;


        public delegate void DualShock4FeedbackReceivedEventHandler(DualShock4FeedbackReceivedEventArgs e);
        public event DualShock4FeedbackReceivedEventHandler FeedbackReceived;

        public OutputControllerDualShock4()
        {
            controller = Program.emClient.CreateDualShock4Controller();
            Init();
        }

        public OutputControllerDualShock4(ushort vendor_id, ushort product_id)
        {
            controller = Program.emClient.CreateDualShock4Controller(vendor_id, product_id);
            Init();
        }

        private void Init()
        {
            controller.AutoSubmitReport = false;
            controller.FeedbackReceived += FeedbackReceivedRcv;
        }

        private void FeedbackReceivedRcv(object _sender, DualShock4FeedbackReceivedEventArgs e)
        {
            FeedbackReceived(e);
        }

        public void Connect()
        {
            controller.Connect();
        }

        public void Disconnect()
        {
            controller.Disconnect();
        }

        public void UpdateInput(OutputControllerDualShock4InputState newState)
        {
            if (currentState != newState)
            {
                DoUpdateInput(newState);
            }
        }

        private void DoUpdateInput(OutputControllerDualShock4InputState new_state)
        {
            controller.SetButtonState(DualShock4Button.Triangle, new_state.triangle);
            controller.SetButtonState(DualShock4Button.Circle, new_state.circle);
            controller.SetButtonState(DualShock4Button.Cross, new_state.cross);
            controller.SetButtonState(DualShock4Button.Square, new_state.square);

            controller.SetButtonState(DualShock4Button.ShoulderLeft, new_state.shoulder_left);
            controller.SetButtonState(DualShock4Button.ShoulderRight, new_state.shoulder_right);

            controller.SetButtonState(DualShock4Button.TriggerLeft, new_state.trigger_left);
            controller.SetButtonState(DualShock4Button.TriggerRight, new_state.trigger_right);

            controller.SetButtonState(DualShock4Button.ThumbLeft, new_state.thumb_left);
            controller.SetButtonState(DualShock4Button.ThumbRight, new_state.thumb_right);

            controller.SetButtonState(DualShock4Button.Share, new_state.share);
            controller.SetButtonState(DualShock4Button.Options, new_state.options);
            controller.SetButtonState(DualShock4SpecialButton.Ps, new_state.ps);
            controller.SetButtonState(DualShock4SpecialButton.Touchpad, new_state.touchpad);

            controller.SetDPadDirection(MapDPadDirection(new_state.dPad));

            controller.SetAxisValue(DualShock4Axis.LeftThumbX, new_state.thumb_left_x);
            controller.SetAxisValue(DualShock4Axis.LeftThumbY, new_state.thumb_left_y);
            controller.SetAxisValue(DualShock4Axis.RightThumbX, new_state.thumb_right_x);
            controller.SetAxisValue(DualShock4Axis.RightThumbY, new_state.thumb_right_y);

            controller.SetSliderValue(DualShock4Slider.LeftTrigger, new_state.trigger_left_value);
            controller.SetSliderValue(DualShock4Slider.RightTrigger, new_state.trigger_right_value);

            controller.SubmitReport();

            currentState = new_state;
        }

        private static DualShock4DPadDirection MapDPadDirection(DpadDirection dPad)
        {
            return dPad switch
            {
                DpadDirection.None => DualShock4DPadDirection.None,
                DpadDirection.North => DualShock4DPadDirection.North,
                DpadDirection.Northeast => DualShock4DPadDirection.Northeast,
                DpadDirection.East => DualShock4DPadDirection.East,
                DpadDirection.Southeast => DualShock4DPadDirection.Southeast,
                DpadDirection.South => DualShock4DPadDirection.South,
                DpadDirection.Southwest => DualShock4DPadDirection.Southwest,
                DpadDirection.West => DualShock4DPadDirection.West,
                DpadDirection.Northwest => DualShock4DPadDirection.Northwest,
                _ => throw new NotImplementedException(),
            };
        }
    }
}
