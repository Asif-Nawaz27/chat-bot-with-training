interface RangeSliderProps {
  label: string;
  value: number;
  min: number;
  max: number;
  step: number;
  onChange: (value: number) => void;
  disabled?: boolean;
  /** Formats the displayed value; defaults to the raw number. */
  format?: (value: number) => string;
}

/** A labeled slider: name on the left, value on the right, teal-filled track below. */
function RangeSlider({ label, value, min, max, step, onChange, disabled, format }: RangeSliderProps) {
  const percent = ((value - min) / (max - min)) * 100;

  return (
    <div className="slider-field">
      <div className="slider-header">
        <span className="slider-label">{label}</span>
        <span className="slider-value mono">{format ? format(value) : value}</span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(Number(e.target.value))}
        className="slider-input"
        style={{
          backgroundImage: `linear-gradient(to right, var(--accent) ${percent}%, var(--surface-sunken) ${percent}%)`,
        }}
      />
    </div>
  );
}

export default RangeSlider;
