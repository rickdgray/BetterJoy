using EvenBetterJoy.Domain.Models;

namespace EvenBetterJoy.Domain.VirtualGamepad
{
    public class OutputControllerDualShock4InputState
    {
        public bool triangle;
        public bool circle;
        public bool cross;
        public bool square;

        public bool trigger_left;
        public bool trigger_right;

        public bool shoulder_left;
        public bool shoulder_right;

        public bool options;
        public bool share;
        public bool ps;
        public bool touchpad;

        public bool thumb_left;
        public bool thumb_right;

        public ControllerDpadDirection dPad;

        public byte thumb_left_x;
        public byte thumb_left_y;
        public byte thumb_right_x;
        public byte thumb_right_y;

        public byte trigger_left_value;
        public byte trigger_right_value;

        public override bool Equals(object other)
        {
            if (other is not OutputControllerDualShock4InputState otherState)
            {
                return false;
            }

            var buttons = triangle == otherState.triangle
                && circle == otherState.circle
                && cross == otherState.cross
                && square == otherState.square
                && trigger_left == otherState.trigger_left
                && trigger_right == otherState.trigger_right
                && shoulder_left == otherState.shoulder_left
                && shoulder_right == otherState.shoulder_right
                && options == otherState.options
                && share == otherState.share
                && ps == otherState.ps
                && touchpad == otherState.touchpad
                && thumb_left == otherState.thumb_left
                && thumb_right == otherState.thumb_right
                && dPad == otherState.dPad;

            var axis = thumb_left_x == otherState.thumb_left_x
                && thumb_left_y == otherState.thumb_left_y
                && thumb_right_x == otherState.thumb_right_x
                && thumb_right_y == otherState.thumb_right_y;

            var triggers = trigger_left_value == otherState.trigger_left_value
                && trigger_right_value == otherState.trigger_right_value;

            return buttons && axis && triggers;
        }

        public override int GetHashCode()
        {
            //TODO: do proper hash
            return base.GetHashCode();
        }
    }
}
