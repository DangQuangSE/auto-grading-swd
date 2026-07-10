export function StateBlock({ title, detail }: { title: string; detail?: string }) {
  return (
    <div className="state-block">
      <strong>{title}</strong>
      {detail ? <p>{detail}</p> : null}
    </div>
  );
}
