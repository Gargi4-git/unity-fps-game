/// <summary>
/// Observer pattern - the object that wants to be told about changes.
/// </summary>
public interface IObserver
{
    void OnSubjectChanged(ISubject subject);
}

/// <summary>
/// Observer pattern - the object that is being watched (the publisher).
/// </summary>
public interface ISubject
{
    void Attach(IObserver observer);
    void Detach(IObserver observer);
    void Notify();
}
