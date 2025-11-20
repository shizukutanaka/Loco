import { lazy, Suspense, ComponentType, ReactNode } from 'react';

export function lazyLoadComponent<P extends object>(
  importFunc: () => Promise<{ default: ComponentType<P> }>,
  displayName = 'LazyComponent',
  fallback?: ReactNode
): ComponentType<P> {
  const LazyComponent = lazy(importFunc);

  const Wrapper = (props: P & any) => (
    <Suspense fallback={fallback || null}>
      <LazyComponent {...props} />
    </Suspense>
  );

  Wrapper.displayName = displayName;
  return Wrapper as ComponentType<P>;
}

interface LazyComponentConfig<P extends object = any> {
  import: () => Promise<{ default: ComponentType<P> }>;
  name: string;
  fallback?: ReactNode;
}

export function createLazyComponents<P extends object = any>(
  configs: LazyComponentConfig<P>[],
  defaultFallback?: ReactNode
): ComponentType<P>[] {
  return configs.map((config) =>
    lazyLoadComponent(config.import, config.name, config.fallback || defaultFallback)
  );
}

export function preloadComponent<P extends object>(
  importFunc: () => Promise<{ default: ComponentType<P> }>
): () => void {
  return () => {
    importFunc().catch((error) => {
      console.error('Failed to preload component:', error);
    });
  };
}

export function createMeasuredLazyComponent<P extends object>(
  importFunc: () => Promise<{ default: ComponentType<P> }>,
  displayName: string,
  fallback?: ReactNode
): ComponentType<P> {
  const measuredImport = async () => {
    const startTime = performance.now();
    const module = await importFunc();
    const endTime = performance.now();
    const loadTime = endTime - startTime;
    console.debug(`[Performance] Component ${displayName} lazy loaded in ${loadTime.toFixed(2)}ms`);
    return module;
  };

  return lazyLoadComponent(measuredImport, displayName, fallback);
}
