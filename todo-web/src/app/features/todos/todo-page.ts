import { Component, inject, OnInit, signal } from '@angular/core';
import { TodoApiService, apiErrorMessage } from '../../core/api/todo-api.service';
import { Todo, TodoWrite } from '../../core/api/todo.model';
import { TodoCard } from './todo-card';
import { TodoEditor } from './todo-editor';

type Filter = 'all' | 'active' | 'completed';

@Component({
  selector: 'app-todo-page',
  standalone: true,
  imports: [TodoCard, TodoEditor],
  templateUrl: './todo-page.html',
  styleUrl: './todo-page.scss'
})
export class TodoPage implements OnInit {
  private readonly api = inject(TodoApiService);
  readonly todos = signal<Todo[]>([]);
  readonly filter = signal<Filter>('all');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly detailLoadingId = signal<string | null>(null);
  readonly detail = signal<Todo | null>(null);
  readonly editorOpen = signal(false);
  readonly editing = signal<Todo | null>(null);
  readonly error = signal<string | null>(null);
  private loadVersion = 0;

  ngOnInit(): void { void this.reload(); }

  async reload(): Promise<void> {
    const version = ++this.loadVersion;
    this.loading.set(true);
    this.error.set(null);
    const filter = this.filter();
    try {
      const rows = await this.api.list(filter === 'all' ? undefined : filter === 'completed');
      if (version === this.loadVersion) this.todos.set(rows);
    } catch (error) {
      if (version === this.loadVersion) this.error.set(apiErrorMessage(error));
    } finally {
      if (version === this.loadVersion) this.loading.set(false);
    }
  }

  setFilter(filter: Filter): void {
    if (filter === this.filter()) return;
    this.filter.set(filter);
    this.detail.set(null);
    void this.reload();
  }

  add(): void {
    this.editing.set(null);
    this.editorOpen.set(true);
    this.error.set(null);
  }

  edit(item: Todo): void {
    this.editing.set(item);
    this.editorOpen.set(true);
    this.error.set(null);
  }

  closeEditor(): void {
    this.editorOpen.set(false);
    this.editing.set(null);
  }

  async save(request: TodoWrite): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      const item = this.editing();
      if (item) await this.api.update(item.id, { ...request, isCompleted: item.isCompleted });
      else await this.api.create(request);
      this.closeEditor();
      await this.reload();
    } catch (error) {
      this.error.set(apiErrorMessage(error));
    } finally {
      this.saving.set(false);
    }
  }

  async toggle(item: Todo): Promise<void> {
    this.busyId.set(item.id);
    this.error.set(null);
    try {
      await this.api.update(item.id, {
        title: item.title, description: item.description, priority: item.priority,
        dueAtUtc: item.dueAtUtc, isCompleted: !item.isCompleted
      });
      this.detail.set(null);
      await this.reload();
    } catch (error) {
      this.error.set(apiErrorMessage(error));
    } finally {
      this.busyId.set(null);
    }
  }

  async view(item: Todo): Promise<void> {
    if (this.detail()?.id === item.id) {
      this.detail.set(null);
      return;
    }
    this.detailLoadingId.set(item.id);
    this.error.set(null);
    try {
      this.detail.set(await this.api.get(item.id));
    } catch (error) {
      this.error.set(apiErrorMessage(error));
    } finally {
      this.detailLoadingId.set(null);
    }
  }

  async remove(item: Todo): Promise<void> {
    if (!window.confirm(`Delete “${item.title}”? This cannot be undone.`)) return;
    this.busyId.set(item.id);
    this.error.set(null);
    try {
      await this.api.delete(item.id);
      this.detail.set(null);
      await this.reload();
    } catch (error) {
      this.error.set(apiErrorMessage(error));
    } finally {
      this.busyId.set(null);
    }
  }
}
