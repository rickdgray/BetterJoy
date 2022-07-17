using EvenBetterJoy.Models;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace EvenBetterJoy.Controller
{
    public class OutputControllerXbox360
    {
        private IXbox360Controller controller;
        private OutputControllerXbox360InputState currentState;

        public delegate void Xbox360FeedbackReceivedEventHandler(Xbox360FeedbackReceivedEventArgs e);

        public event Xbox360FeedbackReceivedEventHandler FeedbackReceived;

        public OutputControllerXbox360()
        {
            controller = Program.emClient.CreateXbox360Controller();
            Init();
        }

        public OutputControllerXbox360(ushort vendor_id, ushort product_id)
        {
            controller = Program.emClient.CreateXbox360Controller(vendor_id, product_id);
            Init();
        }

        private void Init()
        {
            controller.FeedbackReceived += FeedbackReceivedRcv;
            controller.AutoSubmitReport = false;
        }

        private void FeedbackReceivedRcv(object _sender, Xbox360FeedbackReceivedEventArgs e)
        {
            FeedbackReceived(e);
        }

        public void UpdateInput(OutputControllerXbox360InputState newState)
        {
            if (currentState != newState)
            {
                DoUpdateInput(newState);
            }
        }

        public void Connect()
        {
            controller.Connect();
            DoUpdateInput(new OutputControllerXbox360InputState());
        }

        public void Disconnect()
        {
            controller.Disconnect();
        }

        private void DoUpdateInput(OutputControllerXbox360InputState newState)
        {
            controller.SetButtonState(Xbox360Button.LeftThumb, newState.thumb_stick_left);
            controller.SetButtonState(Xbox360Button.RightThumb, newState.thumb_stick_right);

            controller.SetButtonState(Xbox360Button.Y, newState.y);
            controller.SetButtonState(Xbox360Button.X, newState.x);
            controller.SetButtonState(Xbox360Button.B, newState.b);
            controller.SetButtonState(Xbox360Button.A, newState.a);

            controller.SetButtonState(Xbox360Button.Start, newState.start);
            controller.SetButtonState(Xbox360Button.Back, newState.back);
            controller.SetButtonState(Xbox360Button.Guide, newState.guide);

            controller.SetButtonState(Xbox360Button.Up, newState.dpad_up);
            controller.SetButtonState(Xbox360Button.Right, newState.dpad_right);
            controller.SetButtonState(Xbox360Button.Down, newState.dpad_down);
            controller.SetButtonState(Xbox360Button.Left, newState.dpad_left);

            controller.SetButtonState(Xbox360Button.LeftShoulder, newState.shoulder_left);
            controller.SetButtonState(Xbox360Button.RightShoulder, newState.shoulder_right);

            controller.SetAxisValue(Xbox360Axis.LeftThumbX, newState.axis_left_x);
            controller.SetAxisValue(Xbox360Axis.LeftThumbY, newState.axis_left_y);
            controller.SetAxisValue(Xbox360Axis.RightThumbX, newState.axis_right_x);
            controller.SetAxisValue(Xbox360Axis.RightThumbY, newState.axis_right_y);

            controller.SetSliderValue(Xbox360Slider.LeftTrigger, newState.trigger_left);
            controller.SetSliderValue(Xbox360Slider.RightTrigger, newState.trigger_right);

            controller.SubmitReport();

            currentState = newState;
        }
    }
}
