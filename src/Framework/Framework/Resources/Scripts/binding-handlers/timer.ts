type TimerProps = {
    interval: number,
    enabled: KnockoutObservable<boolean>,
    command: () => Promise<DotvvmAfterPostBackEventArgs>
}

ko.virtualElements.allowedBindings["dotvvm-timer"] = true;

export default {
    "dotvvm-timer": {
        init: (element: HTMLElement, valueAccessor: () => TimerProps) => {
            const prop = valueAccessor();
            let timer: number | null = null;

            if (ko.isObservable(prop.enabled)) {
                prop.enabled.subscribe(newValue => createOrDestroyTimer(newValue));
            }
            createOrDestroyTimer(ko.unwrap(prop.enabled));

            function createOrDestroyTimer(enabled: boolean) {
                if (enabled) {
                    if (timer) {
                        window.clearInterval(timer);
                    }

                    timer = window.setInterval(() => {
                        prop.command.bind(element)();
                    }, prop.interval);

                } else if (timer) {
                    window.clearInterval(timer);
                }
            };

            ko.utils.domNodeDisposal.addDisposeCallback(element, () => {
                if (timer) {
                    window.clearInterval(timer);
                }
            });
        }
    }
};
