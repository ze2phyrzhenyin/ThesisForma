/**
 * Block / inline id generation. Schema constraint: ^[A-Za-z][A-Za-z0-9_.-]*$
 * Use a short, sortable, URL-safe id without numbers as the leading char.
 */

let counter = 0;

export function newBlockId(prefix: string): string {
  counter += 1;
  const stamp = Date.now().toString(36);
  const rand = Math.random().toString(36).slice(2, 6);
  return `${prefix}-${stamp}-${rand}-${counter}`;
}
