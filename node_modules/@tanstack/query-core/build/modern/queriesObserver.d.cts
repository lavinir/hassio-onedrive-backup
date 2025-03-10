import { aC as QueryObserverResult, b as QueryClient, aa as QueryObserverOptions, b6 as NotifyOptions, u as Query, c as QueryObserver } from './hydration-BZgfn3GW.cjs';
import { Subscribable } from './subscribable.cjs';
import './removable.cjs';

type QueriesObserverListener = (result: Array<QueryObserverResult>) => void;
type CombineFn<TCombinedResult> = (result: Array<QueryObserverResult>) => TCombinedResult;
interface QueriesObserverOptions<TCombinedResult = Array<QueryObserverResult>> {
    combine?: CombineFn<TCombinedResult>;
}
declare class QueriesObserver<TCombinedResult = Array<QueryObserverResult>> extends Subscribable<QueriesObserverListener> {
    #private;
    constructor(client: QueryClient, queries: Array<QueryObserverOptions<any, any, any, any, any>>, options?: QueriesObserverOptions<TCombinedResult>);
    protected onSubscribe(): void;
    protected onUnsubscribe(): void;
    destroy(): void;
    setQueries(queries: Array<QueryObserverOptions>, options?: QueriesObserverOptions<TCombinedResult>, notifyOptions?: NotifyOptions): void;
    getCurrentResult(): Array<QueryObserverResult>;
    getQueries(): Query<unknown, Error, unknown, readonly unknown[]>[];
    getObservers(): QueryObserver<unknown, Error, unknown, unknown, readonly unknown[]>[];
    getOptimisticResult(queries: Array<QueryObserverOptions>, combine: CombineFn<TCombinedResult> | undefined): [
        rawResult: Array<QueryObserverResult>,
        combineResult: (r?: Array<QueryObserverResult>) => TCombinedResult,
        trackResult: () => Array<QueryObserverResult>
    ];
}

export { QueriesObserver, type QueriesObserverOptions };
