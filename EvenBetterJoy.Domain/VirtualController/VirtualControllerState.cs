namespace EvenBetterJoy.Domain.VirtualController
{
    public class VirtualControllerState
    {
        // buttons
        public bool thumb_stick_left;
        public bool thumb_stick_right;

        public bool y;
        public bool x;
        public bool b;
        public bool a;

        public bool start;
        public bool back;

        public bool guide;

        public bool shoulder_left;
        public bool shoulder_right;

        // dpad
        public bool dpad_up;
        public bool dpad_right;
        public bool dpad_down;
        public bool dpad_left;

        // axis
        public short axis_left_x;
        public short axis_left_y;

        public short axis_right_x;
        public short axis_right_y;

        // triggers
        public byte trigger_left;
        public byte trigger_right;

        public override bool Equals(object other)
        {
            if (other is not VirtualControllerState otherState)
            {
                return false;
            }

            var buttons = thumb_stick_left == otherState.thumb_stick_left
                && thumb_stick_right == otherState.thumb_stick_right
                && y == otherState.y
                && x == otherState.x
                && b == otherState.b
                && a == otherState.a
                && start == otherState.start
                && back == otherState.back
                && guide == otherState.guide
                && shoulder_left == otherState.shoulder_left
                && shoulder_right == otherState.shoulder_right;

            var dpad = dpad_up == otherState.dpad_up
                && dpad_right == otherState.dpad_right
                && dpad_down == otherState.dpad_down
                && dpad_left == otherState.dpad_left;

            var axis = axis_left_x == otherState.axis_left_x
                && axis_left_y == otherState.axis_left_y
                && axis_right_x == otherState.axis_right_x
                && axis_right_y == otherState.axis_right_y;

            var triggers = trigger_left == otherState.trigger_left
                && trigger_right == otherState.trigger_right;

            return buttons && dpad && axis && triggers;
        }

        public static bool operator ==(VirtualControllerState left, VirtualControllerState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VirtualControllerState left, VirtualControllerState right)
        {
            return !(left == right);
        }

        public override int GetHashCode()
        {
            //TODO: use proper hash
            return base.GetHashCode();
        }
    }
}
