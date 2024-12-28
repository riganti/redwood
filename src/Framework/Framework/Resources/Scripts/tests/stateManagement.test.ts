import { fc, waitForEnd } from "./helper";
import dotvvm from '../dotvvm-root'
import { getStateManager } from "../dotvvm-base";
import { lastSetErrorSymbol, StateManager } from "../state-manager";
import { serialize } from "../serialization/serialize";
import { deserialize } from "../serialization/deserialize";
import { serializeDate } from "../serialization/date";
import { areObjectTypesEqual } from "../metadata/typeMap";

require('./stateManagement.data')

const vm = dotvvm.viewModels.root.viewModel as any
const s = getStateManager() as StateManager<any>
s.doUpdateNow()

var warnMock: any;
var printTheWarning = false
beforeEach(() => {
    printTheWarning = false
    warnMock = jest.spyOn(console, 'warn').mockImplementation((...args) => {
        if (printTheWarning) {
            throw new Error("Unexpected warning: " + args.map(a => a instanceof Error || typeof a != "object" ? a : JSON.stringify(a)).join(" "))
        }
    });
});
afterEach(() => {
    warnMock.mockRestore();
});

test("Initial knockout ViewModel", () => {
    expect(vm.Int).observable()
    expect(vm.Array).observableArray()
    expect(vm.ArrayWillBe).not.observableArray()
    expect(vm.ArrayWillBe()).toBeNull()
    expect(vm.Int()).toBe(1)
    expect(vm.Array()[0]).observable()
    expect(vm.Array()[0]().Id).observable()
    expect(vm.Array()[0]().Id()).toBe(1)
    expect(vm.Inner).observable()
    expect(vm.Inner().P1).observable()
    expect(vm.Inner().P2).observable()
    expect(vm.Inner().P3).observable()
    expect(vm.Inner().P1()).toBe(1)
    expect(vm.Inner().P2()).toBe(2)
    expect(vm.Inner().P3()).toBe(3)
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Dirty flag", () => {
    expect(s.isDirty).toBeFalsy()
    s.setState(s.state) // same state should do nothing
    expect(s.isDirty).toBeFalsy()
    s.setState({ ...s.state, Str: "B" })
    expect(s.isDirty).toBeTruthy()
    s.doUpdateNow()
    expect(s.isDirty).toBeFalsy()
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Upgrade null to observableArray", () => {
    printTheWarning = true
    s.update(x => ({ ...x, ArrayWillBe: [{ $type: "t5", B: "ahoj" }] }))
    s.doUpdateNow()

    expect(vm.ArrayWillBe).observableArray()
    expect(vm.ArrayWillBe().length).toBe(1)
    expect(vm.ArrayWillBe()[0]().B).observable()
    expect(vm.ArrayWillBe()[0]().B()).toBe("ahoj")
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Change observableArray to object", () => {
    printTheWarning = true
    // this should not happen IRL, but can when property of type `object` is used in viewModel

    s.update(x => ({ ...x, Array: { $type: "t4", P: "P" } }))
    s.doUpdateNow()

    expect(vm.Array).observable()
    expect(vm.Array().P()).toBe("P")

    s.update(x => ({ ...x, Array: [{ $type: "t2", Id: 17 }] }))
    s.doUpdateNow()

    expect(vm.Array).observableArray()
    expect(vm.Array()[0]).observable()
    expect(vm.Array()[0]().Id).observable()
    expect(vm.Array()[0]().Id()).toBe(17)
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Add and remove type properties", () => {
    printTheWarning = true
    s.update(x => ({ ...x, Inner: { $type: "t3_a", P1: 5, P2: null } }))
    s.doUpdateNow()

    expect(vm.Inner().P1).observable()
    expect(vm.Inner().P1()).toBe(5)
    expect(vm.Inner().P2).observable()
    expect(vm.Inner().P2()).toBe(null)
    expect("P3" in vm.Inner()).toBeFalsy()
    expect("P4" in vm.Inner()).toBeFalsy()

    s.update(x => ({ ...x, Inner: { $type: "t3", P1: 6, P2: 2, P3: 3, P4: 4 } }))
    s.doUpdateNow()

    expect(vm.Inner().P1).observable()
    expect(vm.Inner().P1()).toBe(6)
    expect(vm.Inner().P2).observable()
    expect(vm.Inner().P2()).toBe(2)
    expect(vm.Inner().P3).observable()
    expect(vm.Inner().P3()).toBe(3)
    expect(vm.Inner().P4).observable()
    expect(vm.Inner().P4()).toBe(4)

    s.update(x => ({ ...x, Inner: { $type: "t3_a", P1: 5, P2: null } }))
    s.doUpdateNow()

    expect(vm.Inner().P1).observable()
    expect(vm.Inner().P1()).toBe(5)
    expect(vm.Inner().P2).observable()
    expect(vm.Inner().P2()).toBe(null)
    expect("P3" in vm.Inner()).toBeFalsy()
    expect("P4" in vm.Inner()).toBeFalsy()
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Should not change object reference", () => {
    printTheWarning = true
    const innerObs = vm.Inner
    const innerObj = vm.Inner()

    let changed = false

    innerObs.subscribe(() => changed = true)

    s.update(x => ({ ...x, Inner: { $type: "t3_a", P1: 1, P2: null } }))
    s.doUpdateNow()

    expect(changed).toBeFalsy()
    expect((dotvvm.viewModels.root.viewModel as any).Inner).toBe(innerObs)
    expect((dotvvm.viewModels.root.viewModel as any).Inner()).toBe(innerObj)
    expect(innerObj.P1()).toBe(1)
    expect(innerObj.P2()).toBe(null)
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Should not change array reference", () => {
    printTheWarning = true
    s.setState({ ...s.state, Array: [{ $type: "t2", Id: 1 }] })
    s.doUpdateNow()

    const arrayObs = vm.Array
    const arrayObj = vm.Array()

    let changed = false
    arrayObs.subscribe(() => changed = true)

    s.setState({ ...s.state, Array: [{ $type: "t2", Id: 2 }] })
    s.doUpdateNow()

    expect(changed).toBeFalsy()
    expect((dotvvm.viewModels.root.viewModel as any).Array).toBe(arrayObs)
    expect((dotvvm.viewModels.root.viewModel as any).Array()).toBe(arrayObj)
    expect(arrayObj[0]().Id()).toBe(2)
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Should change array reference when length changes", () => {
    printTheWarning = true
    // this behavior is not strictly needed, we could do with one array
    // However, the changed variable MUST be set to true

    const arrayObs = vm.Array
    const arrayObj = vm.Array()

    let changed = false
    arrayObs.subscribe(() => changed = true, null, "beforeChange")

    s.setState({ ...s.state, Array: [{ $type: "t2", Id: 3 }, { $type: "t2", Id: 4 }] })
    s.doUpdateNow()

    expect(changed).toBeTruthy()
    // observable should be the same, but object should be different
    expect((dotvvm.viewModels.root.viewModel as any).Array).toBe(arrayObs)
    const arrayObj2 = (dotvvm.viewModels.root.viewModel as any).Array()
    expect(arrayObj2).not.toBe(arrayObj) // | these lines are not strictly needed. Rest of the test is essential
    expect(arrayObj.length).toBe(1)      // |
    expect(arrayObj2.length).toBe(2)
    expect(arrayObj2[0]().Id()).toBe(3)
    expect(arrayObj2[1]().Id()).toBe(4)
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Propagate knockout observable change", () => {
    printTheWarning = true

    vm.Inner(null)
    vm.Int(745)
    vm.Array()[0]().Id(500)

    expect(s.state.Inner).toBeNull()
    expect(s.state.Array[0].Id).toBe(500)
    s.update(x => ({ ...x, Int: x.Int + 1 }))
    s.doUpdateNow()
    expect(vm.Int()).toBe(746)
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Propagate knockout object assignment", () => {

    vm.Inner({
        $type: ko.observable("t3"),
        P1: ko.observable(4),
        P2: ko.observable(5),
        P3: 4
    })

    expect(s.state.Inner).toStrictEqual({ $type: "t3", P1: 4, P2: 5, P3: 4, P4: null })
    s.doUpdateNow()
    expect(vm.Inner().P1()).toBe(4)
    expect(vm.Inner().P2()).toBe(5)
    
    expect(warnMock).toHaveBeenCalled();
    expect(warnMock.mock.calls[0][1]).toContain("Replacing old knockout observable with a new one");
})

test("Propagate knockout array assignment", () => {

    vm.ArrayWillBe([
        ko.observable({
            $type: ko.observable("t5"),
            B: ko.observable("hmm")
        })
    ])

    expect(s.state.ArrayWillBe).toStrictEqual([{ $type: "t5", B: "hmm" }])
    s.doUpdateNow()
    expect(vm.ArrayWillBe()[0]().B()).toBe("hmm")
    vm.ArrayWillBe()[0]().B("hmm2")
    expect(s.state.ArrayWillBe).toStrictEqual([{ $type: "t5", B: "hmm2" }])
    
    expect(warnMock).toHaveBeenCalled();
    expect(warnMock.mock.calls[0][1]).toContain("Replacing old knockout observable with a new one");
})

test("Propagate Date assignment", () => {
    printTheWarning = true
    const val = new Date(2000, 3, 3, 3, 3, 3)
    vm.DateTime(val)

    // The date gets converted to DotVVM serialized date format
    expect(s.state.DateTime).toBe(serializeDate(val, false))
    s.doUpdateNow()
    expect(vm.DateTime()).toBe(serializeDate(val, false))

    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("Serialized computed updates on changes", () => {
    printTheWarning = true
    if (ko.options.deferUpdates) {
        // This test won't work this way (i.e. synchronously) with deferUpdate
        return
    }

    const computed = ko.pureComputed(() => serialize(vm))

    let lastValue = null
    computed.subscribe(val => lastValue = val.Str)
    vm.Str("a")
    expect(lastValue).toBe("a")
    s.setState({ ...s.state, Str: "b" })
    s.doUpdateNow()
    expect(lastValue).toBe("b")
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("lastSetError flag", () => {

    // modify value using observable setter
    vm.Int(1);  // valid
    expect(vm.Int[lastSetErrorSymbol]).toBeUndefined();
    expect(() => vm.Int(null)).toThrow();  // invalid
    expect(vm.Int[lastSetErrorSymbol]).toBeDefined();
    vm.Int(2);  // valid
    expect(vm.Int[lastSetErrorSymbol]).toBeUndefined();
    expect(() => vm.Int([])).toThrow();  // invalid
    expect(vm.Int[lastSetErrorSymbol]).toBeDefined();

    // changing state from state manager should reset the flag (if the value is different)
    s.patchState({ Int: 1 });
    s.doUpdateNow();
    expect(vm.Int[lastSetErrorSymbol]).toBeUndefined();

    expect(warnMock).toHaveBeenCalledTimes(2);
    expect(warnMock.mock.calls[0][1]).toContain("Cannot update observable to null");
    expect(warnMock.mock.calls[1][1]).toContain("Cannot update observable to ");
})

test("lastSetError flag - changed back to the original value", () => {

    // modify value using observable setter
    vm.Int(1);  // valid
    expect(vm.Int[lastSetErrorSymbol]).toBeUndefined();
    expect(() => vm.Int(null)).toThrow();  // invalid
    expect(vm.Int[lastSetErrorSymbol]).toBeDefined();

    // changing state from state manager should not reset the flag (if the actual value was not changed)
    s.patchState({ Int: 1 });
    s.doUpdateNow();
    expect(vm.Int[lastSetErrorSymbol]).toBeDefined();

    expect(warnMock).toHaveBeenCalled();
    expect(warnMock.mock.calls[0][1]).toContain("Cannot update observable to null");
})

test("lastSetError flag - triggers observable change even if the value hasn't really changed", () => {
    let changes = 0;
    const subscription = vm.Int.subscribe(() => changes++);
    try {
        expect(changes).toEqual(0);
        vm.Int(2);
        s.doUpdateNow();
        expect(changes).toEqual(1);

        try {
            vm.Int("aaa");
        } catch { }
        s.doUpdateNow();
        expect(changes).toEqual(2);

        vm.Int(2);
        s.doUpdateNow();
        expect(changes).toEqual(3);

        try {
            vm.Int("aaa");
        } catch { }
        s.doUpdateNow();
        expect(changes).toEqual(4);

        s.patchState({ Int: 5 });
        s.doUpdateNow();
        expect(changes).toEqual(5);
    } finally {
        subscription.dispose();
    }

    expect(warnMock).toHaveBeenCalledTimes(2);
    expect(warnMock.mock.calls[0][1]).toContain("Cannot update observable to aaa");
    expect(warnMock.mock.calls[1][1]).toContain("Cannot update observable to aaa");
});

test("coercion happens before assigning to the observable", () => {

    vm.Int(1);
    s.doUpdateNow();
    expect(vm.Int()).toEqual(1);

    vm.Int("3");
    expect(vm.Int()).toEqual(3);
    s.doUpdateNow();
    expect(vm.Int()).toEqual(3);

    expect(() => vm.Int({})).toThrow();

    expect(warnMock).toHaveBeenCalledTimes(1);
    expect(warnMock.mock.calls[0][1]).toContain("Cannot update observable to [object Object]");
})

test("coercion on assigning to observable doesn't unwrap objects", () => {
    printTheWarning = true

    deserialize({
        P1: 13,
        P3: 1
    }, vm.Inner, true);
    expect(vm.Inner().P1).observable();
    expect(vm.Inner().P2).observable();
    expect(vm.Inner().P3).observable();
    expect(vm.Inner().P4).observable();
    const oldInnerValue = vm.Inner();
    const oldInnerP1 = vm.Inner().P1;
    s.doUpdateNow();
    expect(vm.Inner()).toEqual(oldInnerValue);
    expect(vm.Inner().P1).toEqual(oldInnerP1);

    deserialize([
        { Id: 5 }
    ], vm.Array, true);
    expect(vm.Array()[0]).observable();
    expect(vm.Array()[0]().Id).observable();
    const oldArrayValue = vm.Array();
    const oldArrayFirstObjectId = vm.Array()[0]().Id;
    s.doUpdateNow();
    expect(vm.Array()).toEqual(oldArrayValue);
    expect(vm.Array()[0]().Id).toEqual(oldArrayFirstObjectId);

    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("state on the observable", () => {
    printTheWarning = true

    vm.Inner({ P1: 1, P3: 2 });
    s.doUpdateNow();
    const state = vm.Inner.state;
    expect(state).toBeDefined();
    expect(state.P1).toEqual(1);
    expect(state.P2).toBeNull();
    expect(state.P3).toEqual(2);
    expect(state.P4).toBeNull();

    vm.Array([
        { Id: 1 },
        { Id: 3 }
    ]);
    s.doUpdateNow();
    const state2 = vm.Array.state;
    expect(Array.isArray(state2)).toBeTruthy();
    expect(state2.length).toEqual(2);
    expect(state2[0].Id).toEqual(1);
    expect(state2[1].Id).toEqual(3);
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("patchState on the observable", () => {
    printTheWarning = true

    vm.Inner({ P1: 1, P3: 2 });
    s.doUpdateNow();
    vm.Inner.patchState({ P1: 10 });
    const state = vm.Inner.state;
    expect(state).toBeDefined();
    expect(state.P1).toEqual(10);
    expect(state.P2).toBeNull();
    expect(state.P3).toEqual(2);
    expect(state.P4).toBeNull();

    vm.Array([
        { Id: 1 },
        { Id: 3 }
    ]);
    s.doUpdateNow();
    vm.Array.patchState([{}, { Id: 20 }, { Id: 5 }])
    const state2 = vm.Array.state;
    expect(Array.isArray(state2)).toBeTruthy();
    expect(state2.length).toEqual(3);
    expect(state2[0].Id).toEqual(1);
    expect(state2[1].Id).toEqual(20);
    expect(state2[2].Id).toEqual(5);
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("setState on the observable", () => {
    printTheWarning = true

    vm.Inner({ P1: 1, P3: 2 });
    s.doUpdateNow();
    vm.Inner.setState({ P1: 10, P3: 10, P4: 18 });
    const state = vm.Inner.state;
    expect(state).toBeDefined();
    expect(state.P1).toEqual(10);
    expect(state.P2).toBeNull();
    expect(state.P3).toEqual(10);
    expect(state.P4).toEqual(18);

    vm.Array([
        { Id: 1 },
        { Id: 3 }
    ]);
    s.doUpdateNow();
    vm.Array.setState([{ Id: 0 }])
    const state2 = vm.Array.state;
    expect(Array.isArray(state2)).toBeTruthy();
    expect(state2.length).toEqual(1);
    expect(state2[0].Id).toEqual(0);
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("push on observable array - automatic coercion happens", () => {
    printTheWarning = true

    vm.Array([
        { Id: 1 },
        { Id: 3 }
    ]);
    s.doUpdateNow();
    expect(vm.Array().length).toEqual(2);
    vm.Array.push({ Id: 4 });
    expect(vm.Array().length).toEqual(3);

    expect(vm.Array()[2]).observable();
    expect(vm.Array()[2]().Id).observable();
    expect(vm.Array()[2]().Id()).toEqual(4);
    expect(vm.Array()[2]().$type()).toEqual("t2");

    const oldValue = vm.Array()[2];
    s.doUpdateNow();
    expect(vm.Array()[2]).toEqual(oldValue);
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("push on observable array - state is updated immediately", () => {
    printTheWarning = true

    vm.Array([
        { Id: 1 },
        { Id: 3 }
    ]);
    s.doUpdateNow();
    expect(vm.Array().length).toEqual(2);
    
    vm.Array.push({ Id: 4 });
    expect(vm.Array().length).toEqual(3);
    expect(vm.Array.state.length).toEqual(3);
    
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("remove on observable array - state is updated immediately", () => {
    printTheWarning = true

    vm.Array([
        { Id: 1 },
        { Id: 3 },
        { Id: 4 },
    ])
    s.doUpdateNow();
    expect(vm.Array().length).toEqual(3)
    
    vm.Array.remove((i: any) => i().Id() % 2 === 0);
    expect(vm.Array().length).toEqual(2)
    expect(vm.Array.state.length).toEqual(2)

    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("push on observable array - no warning when the new item is not observable", () => {
    printTheWarning = true
    vm.Array([
        { Id: 1 },
        { Id: 3 },
        { Id: 4 },
    ])
    s.doUpdateNow();
    expect(vm.Array().length).toEqual(3)
    
    vm.Array.push({ Id: 5 });
    expect(warnMock).toHaveBeenCalledTimes(0);
})

test("push on observable array - keep warning when the new item is observable", () => {
    vm.Array([
        { Id: 1 },
        { Id: 3 },
        { Id: 4 },
    ])
    s.doUpdateNow();
    expect(vm.Array().length).toEqual(3)
    
    vm.Array.push(ko.observable({ Id: ko.observable(5) }));
    expect(warnMock).toHaveBeenCalled();
    expect(warnMock.mock.calls[0][1]).toContain("Replacing old knockout observable with a new one");
})

test("push on observable array - keep warning when the new item contains observable", () => {
    vm.ArrayWillBe([
        {
            $type: "t5",
            B: "hmm"
        }
    ])
    s.doUpdateNow();
    expect(vm.ArrayWillBe().length).toEqual(1)
    
    vm.ArrayWillBe.push({
        $type: ko.observable("t5"),
        B: ko.observable("hmm")
    });
    expect(warnMock).toHaveBeenCalled();
    expect(warnMock.mock.calls[0].join(" ")).toBe("state-manager Replacing old knockout observable with a new one, just because it is not created by DotVVM. Please do not assign objects with knockout observables into the knockout tree directly. Observable is at /1/$type, value = t5");
})

test("are dynamic types the same - empty objects", () => {
    expect(areObjectTypesEqual({}, {})).toBe(true);
});
test("are dynamic types the same - dynamic vs typed", () => {
    expect(areObjectTypesEqual({}, { $type: "t1" })).toBe(false);
});
test("are dynamic types the same - different property count", () => {
    expect(areObjectTypesEqual({}, { a: "a" })).toBe(false);
});
test("are dynamic types the same - same property count", () => {
    expect(areObjectTypesEqual({ b: "b" }, { a: "a" })).toBe(false);
});
test("are dynamic types the same - same properties", () => {
    expect(areObjectTypesEqual({ a: "b" }, { a: "a" })).toBe(true);
});
test("are dynamic types the same - subset", () => {
    expect(areObjectTypesEqual({ a: "b" }, { a: "a", b: "b" })).toBe(false);
});
test("are dynamic types the same - superset", () => {
    expect(areObjectTypesEqual({ a: "b", b: "a" }, { a: "a" })).toBe(false);
});

test("changing dynamic type property doesn't notify when dynamic types are the same - primitive value", () => {
    vm.Dynamic({ a: "a" });
    s.doUpdateNow();

    let notifyCount = 0;
    const sub = vm.Dynamic.subscribe(() => notifyCount++);
    try {
        vm.Dynamic.setState({ a: "x" });
        s.doUpdateNow();
    }
    finally {
        sub.dispose();
    }
    expect(notifyCount).toBe(0);
});

test("changing dynamic type property doesn't notify when dynamic types are the same - child object", () => {
    vm.Dynamic({ a: { b: "b" } });
    s.doUpdateNow();

    let notifyCount = 0;
    const sub = vm.Dynamic.subscribe(() => notifyCount++);
    try {
        vm.Dynamic.setState({ a: { b: "a", c: "a" } });
        s.doUpdateNow();
    }
    finally {
        sub.dispose();
    }
    expect(notifyCount).toBe(0);
});

test("changing dynamic type property notifies when dynamic types are different - primitive value", () => {
    vm.Dynamic({ a: "a" });
    s.doUpdateNow();

    let notifyCount = 0;
    const sub = vm.Dynamic.subscribe(() => notifyCount++);
    try {
        vm.Dynamic.setState({ a: "a", b: "b" });
        s.doUpdateNow();
    }
    finally {
        sub.dispose();
    }
    expect(notifyCount).toBe(1);
});

test("changing dynamic type property notifies when dynamic types are different - child object", () => {
    vm.Dynamic({ a: { b: "b" } });
    s.doUpdateNow();

    let notifyCount = 0;
    const sub = vm.Dynamic.subscribe(() => notifyCount++);
    try {
        vm.Dynamic.setState({ a: { b: "b" }, b: "b" });
        s.doUpdateNow();
    }
    finally {
        sub.dispose();
    }
    expect(notifyCount).toBe(1);
});

test("state is frozen", () => {
    expect(Object.isFrozen(vm.state)).toBe(true);
    expect(Object.isFrozen(s.state)).toBe(true);
    expect(Object.isFrozen(vm.Dynamic.state)).toBe(true);

    vm.Dynamic.setState({ x: 1 })
    s.doUpdateNow()
    expect(Object.isFrozen(vm.Dynamic.state)).toBe(true);
})

function setupValidatingArraySubscriptions(callback: (arr: DotvvmObservable<{ Id: number }[]>, initial: { Id: number }[], subscribeLog: any[], diffLog: any[]) => void) {
    const arr = vm.Array as DotvvmObservable<{ Id: number }[]>
    expect(arr.push != null && ko.isObservable(arr)).toBe(true)
    const initial: { Id: number }[] = s.state.Array

    function validateElement(item: any, label: number|string) {
        label = `${label} [${ko.toJSON(item)}]`
        if (!ko.isObservable(item))
            throw `Element ${label} is not observable`
        if (!("state" in item))
            throw `Element ${label} does not have state`
        if (!ko.isObservable(item().Id) || !("state" in item().Id))
            throw `Element ${label} does not have Id observable`
        if (typeof item().Id() != "number")
            throw `Element ${label} does not have number Id`
    }

    const eventLog: any[] = []
    const diffLog: any[] = []
    const sub1 = arr.subscribe(newValue => {
        eventLog.push(ko.toJS(newValue))
        
        newValue.forEach(validateElement)
    });
    const sub2 = arr.subscribe(diff => {
        diffLog.push(ko.toJS(diff))
        
        diff.forEach((e, i) => {
            validateElement(e.value, `${e.index}:${e.status}`)
        })

    }, null, "arrayChange")


    try {
        callback(arr, initial, eventLog, diffLog)
    } finally {
        sub1.dispose()
        sub2.dispose()
    }
}

test("ko.observableArray - clones pushed plain observable", () => {
    setupValidatingArraySubscriptions((arr: any, initial) => {
        arr.push(ko.observable({ Id: ko.observable(1) }))

        arr.replace(arr().at(-1)!, ko.observable({ Id: ko.observable(10) }))

        arr.splice(0, 0, ko.observable({ Id: ko.observable(2) }))

        expect(arr.state).toEqual([{ Id: 2, $type: "t2" }, ...initial, { Id: 10, $type: "t2" }])
        expect(arr.state).toEqual(ko.toJS(arr))
    })
})

test("ko.observableArray - clones pushed plain objects", () => {
    setupValidatingArraySubscriptions((arr: any, initial) => {
        arr.push({ Id: 1 })

        arr.replace(arr().at(-1)!, { Id: 10 })

        arr.splice(0, 0, { Id: 2 })

        expect(arr.state).toEqual([{ Id: 2, $type: "t2" }, ...initial, { Id: 10, $type: "t2" }])
        expect(arr.state).toEqual(ko.toJS(arr))
    })
})

test("ko.observableArray - clones pushed mixed observables", () => {
    setupValidatingArraySubscriptions((arr: any, initial) => {
        arr.push(ko.observable({ Id: 1 }))

        arr.replace(arr().at(-1)!, { Id: ko.observable(10) })

        arr.splice(0, 0, ko.observable({ Id: 2 }))

        expect(arr.state).toEqual([{ Id: 2, $type: "t2" }, ...initial, { Id: 10, $type: "t2" }])
        expect(arr.state).toEqual(ko.toJS(arr))
    })
})
test("ko.observableArray - clones pushed dotvvm object taken from elsewhere", () => {
    setupValidatingArraySubscriptions((arr: any, initial) => {
        arr.push({ Id: 1 })
        arr.push(vm.Array().at(-1)())
        arr().at(-1)().Id(2)

        expect(arr.state).toEqual([...initial, { Id: 1, $type: "t2" }, { Id: 2, $type: "t2" }])
        expect(arr.state).toEqual(ko.toJS(arr))
    })
})

// TODO: does not work yet
// test("ko.observableArray - clones pushed dotvvm observable taken from elsewhere", () => {
//     setupValidatingArraySubscriptions((arr: any, initial) => {
//         arr.push({ Id: 1 })
//         arr.push(vm.Array().at(-1))
//         arr().at(-1)().Id(2)

//         expect(arr.state).toEqual([...initial, { Id: 1, $type: "t2" }, { Id: 2, $type: "t2" }])
//         expect(arr.state).toEqual(ko.toJS(arr))
//     })
// })
