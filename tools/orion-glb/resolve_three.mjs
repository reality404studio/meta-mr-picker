const vendor = new URL('./vendor/three/', import.meta.url);

export async function resolve(specifier, context, nextResolve) {
  if (specifier === 'three' && context.parentURL?.startsWith(vendor.href)) {
    return {
      url: new URL('three.module.js', vendor).href,
      format: 'module',
      shortCircuit: true,
    };
  }
  return nextResolve(specifier, context);
}
