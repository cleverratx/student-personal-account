import { ChevronRight, FileText } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useCreateRequest, useRequestTypes, type FieldDef, type FieldSchema, type RequestDetails, type RequestType } from '@/entities/requests';
import { Button, ButtonLink, ErrorState, ScreenSkeleton, TextField, Select } from '@/shared/ui';
import { ScreenHeader } from '@/widgets/app-shell';
import styles from './NewRequestPage.module.css';

// ---- Step 1: type picker ----
function TypePicker({ types, onSelect }: { types: RequestType[]; onSelect: (t: RequestType) => void }) {
  return (
    <div className={styles.pickerBody}>
      <p className={styles.pickerHint}>Выберите, что нужно оформить</p>
      <ul className={styles.typeList}>
        {types.map((type) => (
          <li key={type.id}>
            <button
              type="button"
              className={styles.typeCard}
              onClick={() => onSelect(type)}
              aria-label={type.name}
            >
              <span className={styles.typeIcon} aria-hidden="true">
                <FileText size={20} />
              </span>
              <span className={styles.typeText}>
                <span className={styles.typeName}>{type.name}</span>
                <span className={styles.typeDesc}>{type.description} · {type.processingDays} р.дн.</span>
              </span>
              <ChevronRight size={18} className={styles.typeChevron} aria-hidden="true" />
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

// ---- Step 2: form ----
function FieldInput({ name, def, value, onChange, error }: {
  name: string;
  def: FieldDef;
  value: unknown;
  onChange: (name: string, v: unknown) => void;
  error?: string;
}) {
  const str = (v: unknown) => (v == null ? '' : String(v));

  if (def.type === 'choice' && def.options) {
    return (
      <Select
        label={def.title}
        error={error}
        value={str(value)}
        onChange={(e) => onChange(name, e.target.value || undefined)}
      >
        <option value="">— выберите —</option>
        {def.options.map((opt) => (
          <option key={opt.value} value={opt.value}>{opt.title}</option>
        ))}
      </Select>
    );
  }

  if (def.type === 'integer') {
    return (
      <TextField
        label={def.title}
        error={error}
        inputMode="numeric"
        value={str(value)}
        onChange={(e) => {
          const n = parseInt(e.target.value, 10);
          onChange(name, isNaN(n) ? undefined : n);
        }}
        hint={def.min != null && def.max != null ? `от ${def.min} до ${def.max}` : undefined}
      />
    );
  }

  if (def.type === 'date') {
    return (
      <TextField
        label={def.title}
        error={error}
        type="date"
        value={str(value)}
        onChange={(e) => onChange(name, e.target.value || undefined)}
      />
    );
  }

  // string, uuid — текстовое поле
  return (
    <TextField
      label={def.title}
      error={error}
      value={str(value)}
      maxLength={def.maxLength}
      optional={!def.required}
      onChange={(e) => onChange(name, e.target.value || undefined)}
    />
  );
}

function RequestForm({ type, onSuccess }: { type: RequestType; onSuccess: (r: RequestDetails) => void }) {
  const schema = type.fieldSchema as FieldSchema;
  const { mutate, isPending, error } = useCreateRequest();
  const [fields, setFields] = useState<Record<string, unknown>>({});

  const serverErrors = error?.fieldErrors ?? {};

  function setField(name: string, value: unknown) {
    setFields((prev) => ({ ...prev, [name]: value }));
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    mutate({ requestTypeId: type.id, fields }, { onSuccess });
  }

  return (
    <form onSubmit={handleSubmit} className={styles.form} noValidate>
      <div className={styles.formCard}>
        <p className={styles.formTitle}>{type.name}</p>
        <p className={styles.formDesc}>{type.description}</p>
      </div>

      <div className={styles.fields}>
        {Object.entries(schema).map(([name, def]) => (
          <FieldInput
            key={name}
            name={name}
            def={def}
            value={fields[name]}
            onChange={setField}
            error={serverErrors[`fields.${name}`] ?? serverErrors[name]}
          />
        ))}
      </div>

      {error && !Object.keys(error.fieldErrors).length && (
        <p className={styles.formError} role="alert">{error.message}</p>
      )}

      <Button type="submit" block loading={isPending}>
        Подать заявку
      </Button>
    </form>
  );
}

// ---- Step 3: success ----
function SuccessScreen({ request }: { request: RequestDetails }) {
  const navigate = useNavigate();
  const short = request.id.slice(-8).toUpperCase();
  const ready = request.estimatedReadyDate
    ? (() => {
        const [y, m, d] = request.estimatedReadyDate.split('-');
        return `${d}.${m}.${y}`;
      })()
    : null;

  return (
    <div className={styles.success}>
      <div className={styles.successIcon} aria-hidden="true">✓</div>
      <h2 className={styles.successTitle}>Заявка подана</h2>
      <p className={styles.successId}>№ {short}</p>
      {ready && (
        <p className={styles.successHint}>
          Примерная дата готовности — {ready}.<br />
          Когда будет готово, мы пришлём уведомление.
        </p>
      )}
      <div className={styles.successActions}>
        <Button block onClick={() => void navigate(`/requests/${request.id}`)}>Открыть заявку</Button>
        <ButtonLink variant="secondary" block to="/requests">К списку заявок</ButtonLink>
      </div>
    </div>
  );
}

type Step = { kind: 'pick' } | { kind: 'form'; type: RequestType } | { kind: 'done'; request: RequestDetails };

export function NewRequestPage() {
  const [step, setStep] = useState<Step>({ kind: 'pick' });
  const { data: types, isPending, isError, error, refetch } = useRequestTypes();

  const title =
    step.kind === 'done' ? 'Готово' : step.kind === 'form' ? step.type.name : 'Новая заявка';
  const showBack = step.kind === 'form';

  return (
    <>
      <ScreenHeader
        title={title}
        backTo={showBack ? '/requests' : undefined}
      />

      {step.kind === 'pick' && isPending && <ScreenSkeleton rows={4} label="Загружаем типы заявок" />}
      {step.kind === 'pick' && isError && (
        <ErrorState noun="типы заявок" message={error.message} onRetry={() => void refetch()} />
      )}
      {step.kind === 'pick' && types && (
        <TypePicker types={types} onSelect={(t) => setStep({ kind: 'form', type: t })} />
      )}

      {step.kind === 'form' && (
        <RequestForm
          type={step.type}
          onSuccess={(r) => setStep({ kind: 'done', request: r })}
        />
      )}

      {step.kind === 'done' && <SuccessScreen request={step.request} />}
    </>
  );
}
