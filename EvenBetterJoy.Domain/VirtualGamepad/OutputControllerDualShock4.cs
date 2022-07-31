using EvenBetterJoy.Domain.Models;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace EvenBetterJoy.Domain.VirtualGamepad
{
    public class OutputControllerDualShock4
    {
        private readonly IDualShock4Controller controller;
        private OutputControllerDualShock4InputState currentState;

        public delegate void DualShock4FeedbackReceivedEventHandler(DualShock4FeedbackReceivedEventArgs e);
        public event DualShock4FeedbackReceivedEventHandler FeedbackReceived;

        public OutputControllerDualShock4(ViGEmClient client)
        {
            controller = client.CreateDualShock4Controller();
            controller.FeedbackReceived += FeedbackReceivedRcv;
            controller.AutoSubmitReport = false;
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
            if (currentState == newState)
            {
                return;
            }

            controller.SetButtonState(DualShock4Button.Triangle, newState.triangle);
            controller.SetButtonState(DualShock4Button.Circle, newState.circle);
            controller.SetButtonState(DualShock4Button.Cross, newState.cross);
            controller.SetButtonState(DualShock4Button.Square, newState.square);

            controller.SetButtonState(DualShock4Button.ShoulderLeft, newState.shoulder_left);
            controller.SetButtonState(DualShock4Button.ShoulderRight, newState.shoulder_right);

            controller.SetButtonState(DualShock4Button.TriggerLeft, newState.trigger_left);
            controller.SetButtonState(DualShock4Button.TriggerRight, newState.trigger_right);

            controller.SetButtonState(DualShock4Button.ThumbLeft, newState.thumb_left);
            controller.SetButtonState(DualShock4Button.ThumbRight, newState.thumb_right);

            controller.SetButtonState(DualShock4Button.Share, newState.share);
            controller.SetButtonState(DualShock4Button.Options, newState.options);
            controller.SetButtonState(DualShock4SpecialButton.Ps, newState.ps);
            controller.SetButtonState(DualShock4SpecialButton.Touchpad, newState.touchpad);

            controller.SetDPadDirection(MapDPadDirection(newState.dPad));

            controller.SetAxisValue(DualShock4Axis.LeftThumbX, newState.thumb_left_x);
            controller.SetAxisValue(DualShock4Axis.LeftThumbY, newState.thumb_left_y);
            controller.SetAxisValue(DualShock4Axis.RightThumbX, newState.thumb_right_x);
            controller.SetAxisValue(DualShock4Axis.RightThumbY, newState.thumb_right_y);

            controller.SetSliderValue(DualShock4Slider.LeftTrigger, newState.trigger_left_value);
            controller.SetSliderValue(DualShock4Slider.RightTrigger, newState.trigger_right_value);

            controller.SubmitReport();

            currentState = newState;
        }

        private static DualShock4DPadDirection MapDPadDirection(ControllerDpadDirection direction)
        {
            return direction switch
            {
                ControllerDpadDirection.None => DualShock4DPadDirection.None,
                ControllerDpadDirection.North => DualShock4DPadDirection.North,
                ControllerDpadDirection.Northeast => DualShock4DPadDirection.Northeast,
                ControllerDpadDirection.East => DualShock4DPadDirection.East,
                ControllerDpadDirection.Southeast => DualShock4DPadDirection.Southeast,
                ControllerDpadDirection.South => DualShock4DPadDirection.South,
                ControllerDpadDirection.Southwest => DualShock4DPadDirection.Southwest,
                ControllerDpadDirection.West => DualShock4DPadDirection.West,
                ControllerDpadDirection.Northwest => DualShock4DPadDirection.Northwest,
                _ => throw new NotImplementedException()
            };
        }
    }
}
