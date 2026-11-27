export function downloadCsv(rows: readonly (readonly string[])[], filename: string): void {
  const cell = (value: string) => {
    const safe = /^[\s\uFEFF]*[=+@-]|^[\t\r\n]/.test(value) ? "'" + value : value;
    return '"' + safe.replace(/"/g, '""') + '"';
  };
  const csv = rows.map(row => row.map(cell).join(';')).join('\r\n');
  const url = URL.createObjectURL(new Blob(['\uFEFF', csv], { type: 'text/csv;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
