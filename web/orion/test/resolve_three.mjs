const vendor = new URL('../vendor/three/', import.meta.url);

export async function resolve(specifier, context, nextResolve) {
  if (specifier === 'three') {
    return { url: new URL('three.module.js', vendor).href, format: 'module', shortCircuit: true };
  }
  if (specifier.startsWith('three/addons/')) {
    return { url: new URL(specifier.slice('three/addons/'.length), new URL('addons/', vendor)).href, format: 'module', shortCircuit: true };
  }
  return nextResolve(specifier, context);
}
