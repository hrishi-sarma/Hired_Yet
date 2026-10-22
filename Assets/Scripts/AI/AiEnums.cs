public enum AgentKind { Pedestrian, Car }

public enum ItemRarity { Common, Uncommon, Rare }

public enum PedState { Walk, Idle, WaitAtCurb, Cross, Yield, EnterHouse, Alert }

public enum CarState { Drive, WaitIntersection, Blocked, Honking, Overtaking, Charging, WaitSignal, RunningRed }

public enum SignalState { Green, Amber, Red, Flash }

public enum Impairment { None, Drunk, Sleepy }

public enum MemEvent { CutOff, NearMiss, Blocked, Honked, RanRed }
