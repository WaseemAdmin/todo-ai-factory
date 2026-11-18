import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { ReactiveFormsModule, AbstractControl, FormControl, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { Todo, TodoPriority, TodoWrite } from '../../core/api/todo.model';

@Component({
  selector: 'app-todo-editor',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './todo-editor.html',
  styleUrl: './todo-editor.scss'
})
export class TodoEditor implements OnChanges {
  @Input() item: Todo | null = null;
  @Input() busy = false;
  @Output() save = new EventEmitter<TodoWrite>();
  @Output() cancel = new EventEmitter<void>();

  readonly form = new FormGroup({
    title: new FormControl('', { nonNullable: true, validators: [trimmedTitleValidator] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    priority: new FormControl<TodoPriority>('Normal', { nonNullable: true, validators: [Validators.required] }),
    dueAtLocal: new FormControl('', { nonNullable: true })
  });

  ngOnChanges(): void {
    this.form.reset({
      title: this.item?.title ?? '',
      description: this.item?.description ?? '',
      priority: this.item?.priority ?? 'Normal',
      dueAtLocal: this.item?.dueAtUtc ? toLocalDateTime(this.item.dueAtUtc) : ''
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.save.emit({
      title: value.title.trim(),
      description: value.description || null,
      priority: value.priority,
      dueAtUtc: value.dueAtLocal ? new Date(value.dueAtLocal).toISOString() : null
    });
  }
}

function trimmedTitleValidator(control: AbstractControl): ValidationErrors | null {
  const length = String(control.value ?? '').trim().length;
  return length >= 1 && length <= 200 ? null : { titleLength: true };
}

function toLocalDateTime(utc: string): string {
  const date = new Date(utc);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}
